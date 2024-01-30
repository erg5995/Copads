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
    static class CopadsExample1 {

        static int simultaneousThreads = 1;
        public static List<string> CollectDirectories(string rootDir) {
            ConcurrentBag<string> dirs = new ConcurrentBag<string>(Directory.GetDirectories(rootDir));

            Parallel.ForEach(dirs, new ParallelOptions {MaxDegreeOfParallelism = simultaneousThreads}, subdir => {
                List<string> subdirs = CollectDirectories(subdir);
                dirs.AddAll(subdirs);
            });

            return new List<string>(dirs);
        }

        public static List<string> CollectFiles(List<string> dirs) {
            ConcurrentBag<string> files = new ConcurrentBag<string>();

            Parallel.ForEach(dirs, new ParallelOptions {MaxDegreeOfParallelism = simultaneousThreads}, dir => {
                files.AddAll(new List<string>(Directory.GetFiles(dir)));
            });
            return new List<string>(files);
        }

        public static void Main(string[] args) {

            int maxThreads = 10;
      
            if (args.Length < 1 || args.Length > 2) {
                Console.WriteLine("Usage: dotnet run directory_path [max_threads]");
                return;
            }

            if (!Directory.Exists(args[0])) {
                Console.WriteLine($"The directory '{args[0]}' does not exist.");
                return;
            }

            bool validArg = int.TryParse(args[1], out maxThreads);

            if(!validArg || maxThreads < 1) {
                Console.WriteLine($"'{args[1]}' is not a positive integer");
            }

            string rootDir = args[0];

            for(; simultaneousThreads <= maxThreads; simultaneousThreads++) {
                test(rootDir);
            }

            
        }

        public static void test(string rootDir) {

            Console.WriteLine($"Testing file size index speed in {rootDir} with {simultaneousThreads} simultaneous threads");

            long totalSize = 0;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<string> dirs = CollectDirectories(rootDir);
            List<string> files = CollectFiles(dirs);

            Parallel.ForEach(files, new ParallelOptions {MaxDegreeOfParallelism = simultaneousThreads}, file => {
                long size = new FileInfo(file).Length;
                Interlocked.Add(ref totalSize, size);
            });

            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = string.Format(
                "{0:00}:{1:00}.{2:000}",
                ts.Minutes, ts.Seconds, ts.Milliseconds
            );
            Console.WriteLine($"\tTime to index size: {elapsedTime}");
            Console.WriteLine($"\tTotal Size: {totalSize}B\n");
        }
    }
}



