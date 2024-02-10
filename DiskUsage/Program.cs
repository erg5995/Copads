using System.Collections.Concurrent;

namespace Extensions {
    static class Extensions {
        public static void AddAll<T>(this ConcurrentBag<T> bag, List<T> items) {
            foreach(T item in items) {
                bag.Add(item);
            }
        }
    }
}

namespace CopadsExample {
    using System.Diagnostics;
    using Extensions;

    static class ArgumentHelper {

        // Help message conforms to Microsoft Command-Line Syntax: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/command-line-syntax-key
        private static readonly string helpMessage = 
        @"Usage: dotnet run {-s|-d|-b} path
        -s      Run in single threaded mode
        -d      Run in parallel mode (uses all available processors)
        -b      Run in both parallel and single threaded mode.
                Runs parallel followed by sequential mode";

        public static void ErrorOut(string message) {
            Console.WriteLine(message + "\n" + helpMessage);
            Environment.Exit(0);
        }

        public static void ValidateArgumentList(string[] args) {
            if (args.Length < 1 || args.Length > 2) {
                ErrorOut("Argument list does not match required number of arguments.");
            }
        }

        public static string ValidateParallelism(string parallelism) {
            if(!(parallelism == "-s" || parallelism == "-d" || parallelism == "-b")) {
                ErrorOut($"Invalid flag: {parallelism}");
            }
            return parallelism;
        }

        public static string ValidatePath(string path) {
            FileInfo fi = null;
            try {
                fi = new FileInfo(path);
            }
            catch (Exception ex) when (ex is PathTooLongException or NotSupportedException or ArgumentException) {
                ErrorOut($"The path '{path}' is not a valid pathname.");
            }

            if(!Directory.Exists(path)) {
                ErrorOut($"The directory '{path}' does not exist.");
            }

            return path;
        }

    }
    static class DiskUsage {

        public static List<string> CollectDirectories(string rootDir, bool parallel) {
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
        }

        public static List<string> CollectFiles(List<string> dirs, bool parallel) {
            ConcurrentBag<string> files = new ConcurrentBag<string>();

            if(parallel) {
                Parallel.ForEach(dirs, dir => {
                    files.AddAll(new List<string>(Directory.GetFiles(dir)));
                });
            } else {
                foreach(string dir in dirs) {
                    files.AddAll(new List<string>(Directory.GetFiles(dir)));
                }
            }
            return new List<string>(files);
        }

        public static void Main(string[] args) {

            ArgumentHelper.ValidateArgumentList(args);
            //0 = -s, 1 = -d, 2 = -b
            string parallelism = ArgumentHelper.ValidateParallelism(args[0]);
            string rootDir = ArgumentHelper.ValidatePath(args[1]);

            if(parallelism == "-s") {
                CalculateDiskUsage(rootDir, false);
            }
            else if(parallelism == "-d") {
                CalculateDiskUsage(rootDir, true);
            } else if(parallelism == "-b") {
                CalculateDiskUsage(rootDir, false);
                CalculateDiskUsage(rootDir, true);
            }
   
        }

        public static void CalculateDiskUsage(string rootDir, bool parallel) {

            long totalSize = 0;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<string> dirs = CollectDirectories(rootDir, parallel);
            List<string> files = CollectFiles(dirs, parallel);

            if(parallel) {
                Parallel.ForEach(files, file => {
                    long size = new FileInfo(file).Length;
                    Interlocked.Add(ref totalSize, size);
                });
            } else {
                foreach(string file in files) {
                    long size = new FileInfo(file).Length;
                    totalSize += size;
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
        }
    }
}



