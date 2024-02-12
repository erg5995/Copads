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

        public static readonly string[] IMAGE_EXTENSIONS = new string[] {
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

        public static bool DisplayWarnings = false;

        public static long TotalSize = 0;
        public static int NumFiles = 0; 
        public static long ImageSize = 0;
        public static int NumImages = 0;
        public static int NumFolders = 0;


        public static void IndexDir(string dir, bool parallel) {
            List<string> files = new List<string>();
            List<string> subdirs = new List<string>();
            try {
                files = new List<string>(Directory.GetFiles(dir));
                subdirs = new List<string>(Directory.GetDirectories(dir));
            } catch(Exception e) when (e is UnauthorizedAccessException or IOException or AggregateException) {
                if(DisplayWarnings)
                    Console.WriteLine($"Warning: Permission not granted to read from {dir}");
                return;
            }
            
            if(parallel) {
                Interlocked.Increment(ref NumFolders);
                Parallel.ForEach(files, (file) => {
                    Interlocked.Increment(ref NumFiles);
                    long length = new FileInfo(file).Length;
                    if(IMAGE_EXTENSIONS.Contains(file.Split('.').Last())) {
                        Interlocked.Add(ref ImageSize, length);
                        Interlocked.Increment(ref NumImages);
                    }
                    Interlocked.Add(ref TotalSize, length);
                });
                
                Parallel.ForEach(subdirs, (subdir) => {
                    IndexDir(subdir, parallel);
                });
            } else {
                NumFolders++;
                foreach(string file in files) {
                    long length = new FileInfo(file).Length;
                    if(IMAGE_EXTENSIONS.Contains(file.Split('.').Last())) {
                        ImageSize += length;
                        NumImages++;
                    }
                    TotalSize += length;
                } 
                foreach(string subdir in subdirs) {
                    IndexDir(subdir, parallel);
                }
            }
        }

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
                CalculateDiskUsageWithCache(rootDir, false);
                CalculateDiskUsageWithAccumulation(rootDir, false);
            } else if(parallelism == "-d") {
                CalculateDiskUsageWithCache(rootDir, true);
                CalculateDiskUsageWithAccumulation(rootDir, true);
            } else if(parallelism == "-b") {
                CalculateDiskUsageWithCache(rootDir, true);
                CalculateDiskUsageWithAccumulation(rootDir, true);
                CalculateDiskUsageWithCache(rootDir, false);
                CalculateDiskUsageWithAccumulation(rootDir, false);
            }
   
        }

        public static void CalculateDiskUsageWithCache(string rootDir, bool parallel) {

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<string> dirs = CollectDirectories(rootDir, parallel);
            dirs.Add(rootDir);
            List<string> files = CollectFiles(dirs, parallel);

            if(parallel) {
                Parallel.ForEach(files, file => {
                    try {
                        long length = new FileInfo(file).Length;
                        string file_extension = file.Split(".").Last();
                        if(IMAGE_EXTENSIONS.Contains("." + file_extension)) {
                            Interlocked.Add(ref ImageSize, length);
                            Interlocked.Increment(ref NumImages);
                        }
                        Interlocked.Add(ref TotalSize, length);
                    } catch (IOException) {
                        if(DisplayWarnings)
                            Console.WriteLine($"Warning: {file} was moved during indexing.");
                    }
                });
            } else {
                foreach(string file in files) {
                    try {
                        long size = new FileInfo(file).Length;
                        TotalSize += size;
                    } catch (IOException) {
                        if(DisplayWarnings)
                            Console.WriteLine($"Warning {file} was moved during indexing.");
                    }
                }
            }

            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = string.Format(
                "{0:000}s.{1:000}ms",
                ts.Seconds + (60 * ts.Minutes), ts.Milliseconds
            );
            Console.WriteLine($"{(parallel ? "Parallel" : "Sequential")} (Cache Method) Calculated in: {elapsedTime}");
            Console.WriteLine($"{dirs.Count:n0} folders, {files.Count:n0} files, {TotalSize:n0} bytes");
            if(NumImages > 0) {
                Console.WriteLine($"{NumImages:n0} image files: {ImageSize:n0} bytes");
            } else {
                Console.WriteLine($"No image files found under {rootDir}");
            }
            Reset();
        }

        public static void CalculateDiskUsageWithAccumulation(string rootDir, bool parallel) {

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            IndexDir(rootDir, parallel);

            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = string.Format(
                "{0:000}s.{1:000}ms",
                ts.Seconds + (60 * ts.Minutes), ts.Milliseconds
            );
            Console.WriteLine($"{(parallel ? "Parallel" : "Sequential")} (Accumulate Method) Calculated in: {elapsedTime}");
            Console.WriteLine($"{NumFolders:n0} folders, {NumFiles:n0} files, {TotalSize:n0} bytes");
            if(NumImages > 0) {
                Console.WriteLine($"{NumImages:n0} image files: {ImageSize:n0} bytes");
            } else {
                Console.WriteLine($"No image files found under {rootDir}");
            }
            Reset();
        }

        public static void Reset() {
            NumFiles = 0;
            NumImages = 0;
            NumFolders = 0;
            TotalSize = 0;
            ImageSize = 0;
            Console.WriteLine();
        }
    }
}



