using System.Collections.Concurrent;

namespace Extensions {

    using DiskUsage;
    static class Extensions {
        public static void AddAll<T>(this ConcurrentBag<T> bag, List<T> items) {
            foreach(T item in items) {
                bag.Add(item);
            }
        }

        public static void SafeAddAllFiles(this ConcurrentBag<string> bag, string dir) {
            List<string> files = new List<string>();
            try {
                files.AddRange(Directory.GetFiles(dir));
            } catch(Exception e) when (e is UnauthorizedAccessException or IOException or AggregateException) {
                if(DiskUsage.DisplayWarnings)
                Console.WriteLine($"Warning: Permission not granted to read from {dir}");
            }
            bag.AddAll(files);
        }
    }
}

namespace DiskUsage {
    using System.Diagnostics;
    using Extensions;

    static class ArgumentHelper {

        // Help message conforms to Microsoft Command-Line Syntax: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/command-line-syntax-key
        private static readonly string helpMessage = 
        @"Usage: dotnet run [-w] {-s|-d|-b} path
        -w      Display warnings when files/directories cannot be read (optional)
        -s      Run in single threaded mode
        -d      Run in parallel mode (uses all available processors)
        -b      Run in both parallel and single threaded mode.
                Runs parallel followed by sequential mode
        path    Root directory to index from";

        public static void ErrorOut(string message) {
            Console.WriteLine(message + "\n" + helpMessage);
            Environment.Exit(0);
        }

        public static int ValidateArgumentList(string[] args) {
            if (args.Length < 2 || args.Length > 3) {
                ErrorOut("Actual and required argument lists differ in length.");
            }
            return args.Length;
        }

        public static void ValidateWarningsFlag(string warnings) {
            if(warnings != "-w") {
                ErrorOut($"Invalid option:{warnings}\nValid options are: -w");
            } else {
                DiskUsage.DisplayWarnings = true;
            }
        }

        public static string ValidateParallelismFlag(string parallelism) {
            if(!(parallelism == "-s" || parallelism == "-d" || parallelism == "-b")) {
                ErrorOut($"Invalid flag: {parallelism}");
            }
            return parallelism;
        }

        public static string ValidatePath(string path) {
            try {
                FileInfo fi = new FileInfo(path);
            }
            catch (Exception e) when (e is PathTooLongException or NotSupportedException or ArgumentException) {
                ErrorOut($"The path '{path}' is not a valid pathname.");
            }

            if(!Directory.Exists(path)) {
                ErrorOut($"The directory '{path}' does not exist.");
            }

            return path;
        }

    }
    static class DiskUsage {

        public static bool DisplayWarnings = false;

        public static List<string> CollectDirectories(string rootDir, bool parallel) {
            try {
                ConcurrentBag<string> dirs = new ConcurrentBag<string>(Directory.GetDirectories(rootDir));

                if(parallel) {
                    Parallel.ForEach(dirs, subdir => {
                        List<string> subdirs = CollectDirectories(subdir, parallel);
                        dirs.AddAll(subdirs);
                    });
                } else {
                    foreach(string subdir in dirs) {
                        List<string> subdirs = CollectDirectories(subdir, parallel);
                        dirs.AddAll(subdirs);
                    }
                }
                return new List<string>(dirs);
            } catch(Exception e) when (e is UnauthorizedAccessException or IOException or AggregateException) {
                return new List<string>();
            }
        }

        public static List<string> CollectFiles(List<string> dirs, bool parallel) {
            ConcurrentBag<string> files = new ConcurrentBag<string>();

            if(parallel) {
                Parallel.ForEach(dirs, dir => {
                    files.SafeAddAllFiles(dir);
                });
            } else {
                foreach(string dir in dirs) {
                    files.SafeAddAllFiles(dir);
                }
            }
            return new List<string>(files);
        }

        public static void Main(string[] args) {

            int numArguments = ArgumentHelper.ValidateArgumentList(args);
            int i = numArguments == 3 ? 0 : 1;
            if(i == 0) {
                ArgumentHelper.ValidateWarningsFlag(args[0]);
            }
            string parallelism = ArgumentHelper.ValidateParallelismFlag(args[1 - i]);
            string rootDir = ArgumentHelper.ValidatePath(args[2 - i]);
            

            if(parallelism == "-s") {
                CalculateDiskUsage(rootDir, false);
            } else if(parallelism == "-d") {
                CalculateDiskUsage(rootDir, true);
            } else if(parallelism == "-b") {
                CalculateDiskUsage(rootDir, false);
                CalculateDiskUsage(rootDir, true);
            }
   
        }

        public static void CalculateDiskUsage(string rootDir, bool parallel) {

            long totalSize = 0;
            long imageSize = 0;
            int images = 0;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<string> dirs = CollectDirectories(rootDir, parallel);
            List<string> files = CollectFiles(dirs, parallel);

            string[] image_extensions = new string[] {
                ".jpg",
                ".jpeg",
                ".png",
                ".gif",
                ".tiff",
                ".bmp",
                ".svg",
                ".webp",
                ".heif",
                ".heic",
                ".raw",
                ".cr2",
                ".nef",
                ".orf",
                ".sr2",
                ".arw",
                ".dng",
                ".eps",
                ".ico",
                ".jfif",
                ".psd",
                ".tif",
                ".xcf",
                ".ai",
                ".cdr",
                ".indd",
                ".webm"
            };


            if(parallel) {
                Parallel.ForEach(files, file => {
                    try {
                        long size = new FileInfo(file).Length;
                        string file_extension = file.Split(".").Last();
                        if(image_extensions.Contains("." + file_extension)) {
                            Interlocked.Add(ref imageSize, size);
                            Interlocked.Increment(ref images);
                        }
                        Interlocked.Add(ref totalSize, size);
                    } catch (IOException) {
                        if(DisplayWarnings)
                            Console.WriteLine($"Warning: {file} was moved during indexing.");
                    }
                });
            } else {
                foreach(string file in files) {
                    try {
                        long size = new FileInfo(file).Length;
                        totalSize += size;
                    } catch (IOException) {
                        if(DisplayWarnings)
                            Console.WriteLine($"Warning {file} was moved during indexing.");
                    }
                }
            }

            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = string.Format(
                "{0:00}s.{1:000}ms",
                ts.Seconds, ts.Milliseconds
            );
            Console.WriteLine($"{(parallel ? "Parallel" : "Sequential")} Calculated in: {elapsedTime}");
            Console.WriteLine($"{dirs.Count:n0} folders, {files.Count:n0} files, {totalSize:n0} bytes");
            if(images > 0) {
                Console.WriteLine($"\n{images:n0} image files: {imageSize:n0} bytes");
            } else {
                Console.WriteLine($"No image files found under {rootDir}");
            }
        }
    }
}



