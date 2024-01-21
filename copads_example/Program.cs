using System.Collections.Concurrent;

CopadsExample.CopadsExample1.Main(args);

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

    using Extensions;
    static class CopadsExample1 {
        public static List<string> CollectDirectories(string rootDir) {
            ConcurrentBag<string> dirs = new ConcurrentBag<string>(Directory.GetDirectories(rootDir));

            Parallel.ForEach(dirs, subdir => {
                List<string> subdirs = CollectDirectories(subdir);
                dirs.AddAll(subdirs);
            });

            return new List<string>(dirs);
        }

        public static List<string> CollectFiles(List<string> dirs) {
            ConcurrentBag<string> files = new ConcurrentBag<string>();

            Parallel.ForEach(dirs, dir => {
                files.AddAll(new List<string>(Directory.GetFiles(dir)));
            });
            return new List<string>(files);
        }

        public static void Main(string[] args) {
      
            if (args.Length == 0) {
                Console.WriteLine("There are no command line arguments.");
                return;
            }

            if (! Directory.Exists(args[0])) {
                Console.WriteLine("The directory does not exist.");
                return;
            }

            string rootDir = args[0];

            List<string> dirs = CollectDirectories(rootDir);
            List<string> files = CollectFiles(dirs);

            long totalSize = 0;

            Parallel.ForEach(files, file => {
                long size = new FileInfo(file).Length;
                Interlocked.Add(ref totalSize, size);
            });

            Console.WriteLine($"Total Size: {totalSize}B");
        }
    }
}



