﻿using System.Collections.Concurrent;

namespace Extensions {
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
            } catch(UnauthorizedAccessException uae) {
                Console.WriteLine($"Warning: Permission not granted to read from {dir}");
            }
            bag.AddAll(files);
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
            try {
                FileInfo fi = new FileInfo(path);
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
            } catch(UnauthorizedAccessException uae) {
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

            ArgumentHelper.ValidateArgumentList(args);
            string parallelism = ArgumentHelper.ValidateParallelism(args[0]);
            string rootDir = ArgumentHelper.ValidatePath(args[1]);

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
                    long size = new FileInfo(file).Length;
                    string file_extension = file.Split(".").Last();
                    if(image_extensions.Contains("." + file_extension)) {
                        Interlocked.Add(ref imageSize, size);
                        Interlocked.Increment(ref images);
                    }
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
            Console.WriteLine($"\n{images:n0} image files: {imageSize:n0} bytes");
        }
    }
}



