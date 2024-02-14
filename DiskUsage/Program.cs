using System.Collections.Concurrent;

namespace Extensions {
    static class DirectoryWrapper {

        public static List<string> SafeGetFiles(string dir) {
            try {
                return new List<string>(Directory.GetFiles(dir, "*", new EnumerationOptions {AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint}));
            } catch(Exception ex) when (ex is UnauthorizedAccessException or IOException or AggregateException) {
                if(DiskUsage.DiskUsage.DisplayWarnings) {
                    Console.WriteLine($"Warning: Permission not granted to read files from {dir}");
                }
                return new List<string>();
            }
        }

        public static List<string> SafeGetDirectories(string dir) {
            try {
                return new List<string>(Directory.GetDirectories(dir, "*"));
            } catch(Exception ex) when (ex is UnauthorizedAccessException or IOException or AggregateException) {
                if(DiskUsage.DiskUsage.DisplayWarnings) {
                    Console.WriteLine($"Warning: Permission not granted to read directories from {dir}");
                }
                return new List<string>();
            }
        }

    }

    static class FileInfoWrapper {
        public static long Length(string path) {
            try {
                return new FileInfo(path).Length;
            } catch(Exception ex) when (ex is UnauthorizedAccessException or IOException or AggregateException) {
                if(DiskUsage.DiskUsage.DisplayWarnings) {
                    Console.WriteLine($"Warning: size of {path} could not be accessed");
                }
                return 0L;
            }
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


        public static void IndexDirParallel(string dir) {
            List<string> files = DirectoryWrapper.SafeGetFiles(dir);
            List<string> subdirs = DirectoryWrapper.SafeGetDirectories(dir);
            
            Interlocked.Increment(ref NumFolders);
            Parallel.ForEach(files, (file) => {
                Interlocked.Increment(ref NumFiles);
                long length = new FileInfo(file).Length;
                if(IMAGE_EXTENSIONS.Contains("." + file.Split('.').Last())) {
                    Interlocked.Add(ref ImageSize, length);
                    Interlocked.Increment(ref NumImages);
                }
                Interlocked.Add(ref TotalSize, length);
            });
            
            Parallel.ForEach(subdirs, IndexDirParallel);
        }

        public static void IndexDirSequential(string dir) {
            List<string> files = DirectoryWrapper.SafeGetFiles(dir);
            List<string> subdirs = DirectoryWrapper.SafeGetDirectories(dir);

            NumFolders++;
            files.ForEach(file => {
                NumFiles++;
                long length = new FileInfo(file).Length;
                if(IMAGE_EXTENSIONS.Contains("." + file.Split('.').Last())) {
                    ImageSize += length;
                    NumImages++;
                }
                TotalSize += length;
            });
            subdirs.ForEach(IndexDirSequential);
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
                CalculateDiskUsage(rootDir, true);
                CalculateDiskUsage(rootDir, false);
            }
        }

        public static void CalculateDiskUsage(string rootDir, bool parallel) {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            if(parallel) {
                IndexDirParallel(rootDir);
            } else {
                IndexDirSequential(rootDir);
            }

            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = string.Format(
                "{0:000}s.{1:000}ms",
                ts.Seconds + (60 * ts.Minutes), ts.Milliseconds
            );
            Console.WriteLine($"{(parallel ? "Parallel" : "Sequential")} Calculated in: {elapsedTime}");
            Console.WriteLine($"{NumFolders:n0} folders, {NumFiles:n0} files, {TotalSize:n0} bytes");
            if(NumImages > 0) {
                Console.WriteLine($"{NumImages:n0} image files: {ImageSize:n0} bytes");
            } else {
                Console.WriteLine($"No image files found under {rootDir}");
            }
            Reset();
        }

        private static void Reset() {
            NumFiles = 0;
            NumImages = 0;
            NumFolders = 0;
            TotalSize = 0;
            ImageSize = 0;
            Console.WriteLine();
        }
    }
}