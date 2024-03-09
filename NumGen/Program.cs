

using System.Numerics;

namespace Extensions {

    /*
    Contains some extension methods for working with BigIntegers and and the Miller-Rabin primality test
    */
    public static class BigIntegerExtensions {
        /*
        Modified from https://stackoverflow.com/questions/3432412/calculate-square-root-of-a-biginteger-system-numerics-biginteger
        Find the square root of a BigInteger
        */
        public static BigInteger Sqrt(this BigInteger n) {
            if (n == 0)
                return 0;
            if (n > 0) {
                int bitLength = Convert.ToInt32(Math.Ceiling(BigInteger.Log(n, 2)));
                BigInteger root = BigInteger.One << (bitLength / 2);

                while (!isSqrt(n, root)) {
                    root += n / root;
                    root >>= 1;
                }

                return root;
            }

            Console.WriteLine(n);
            throw new ArithmeticException("NaN");
        }

        private static bool isSqrt(BigInteger n, BigInteger root)
        {
            BigInteger lowerBound = root*root;
            return n >= lowerBound && n <= lowerBound + root + root;
        }

        /*
        Modified from https://stackoverflow.com/a/68593532/13471744
        Generate a new BigInteger between given bounds
        */
        public static BigInteger NextBigInteger(BigInteger minValue, BigInteger maxValue) {
            if (minValue > maxValue) throw new ArgumentException();
            if (minValue == maxValue) return minValue;
            BigInteger zeroBasedUpperBound = maxValue - 1 - minValue; // Inclusive
            byte[] bytes = zeroBasedUpperBound.ToByteArray();

            byte lastByteMask = 0b11111111;
            for (byte mask = 0b10000000; mask > 0; mask >>= 1, lastByteMask >>= 1)
            {
                if ((bytes[bytes.Length - 1] & mask) == mask) break;
            }

            while (true)
            {
                System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(bytes);
                bytes[bytes.Length - 1] &= lastByteMask;
                var result = new BigInteger(bytes);
                if (result <= zeroBasedUpperBound) return result + minValue;
            }
        }

        /*
        Quickly generate a positive BigInteger
        */
        public static BigInteger NextPositiveBigInteger(int numBytes) {
            byte[] bytes = new byte[numBytes];
            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(bytes);
            //To get a positive number we have to brownout the first
            //bit (sign bit) of the MSB
            //For some reason BigInteger stores bytes in little endian,
            //so the MSB is the rightmost byte
            bytes[numBytes-1] &= 0b01111111;

            return new BigInteger(bytes);
        }

        /*
        Miller-Rabin primality test
        */
        public static bool isProbablyPrime(this BigInteger num) {
            int k = 5;
            if ((num < 2) || (num % 2 == 0)) return num == 2;

            BigInteger d = BigInteger.Subtract(num, BigInteger.One);
            int s = 0;
            while (d % 2 == 0) {
                d >>= 1; //Factor powers of 2 from n - 1
                s++;
            }

            for (int i = 0; i < k; i++)
            {
                BigInteger a = NextBigInteger(2, num - 1);
                BigInteger x = BigInteger.ModPow(a, d, num);
                BigInteger y = BigInteger.One;
                for(int n = 0; n < s; n++) {
                    y = BigInteger.ModPow(x, 2, num);
                    if(y == 1 && x != 1 && x != num - 1) {
                        return false;
                    }
                    x = y;
                }
                if(y != 1) {
                    return false;
                }
            }
            return true;
        }
    }
}

namespace NumGen {
    using System.Diagnostics;
    using System.Text;
    using Extensions;

    /*
    My wrapper implementation of StringBuilder, which provides
    a Thread-Safe way for concurrently running prime generators to
    post their results. Also disallows adding more results once
    the specified count has been reached.
    */
    public class StringBuilderWrapper {
        private int count, max;
        private StringBuilder sb;

        public StringBuilderWrapper(int max) {
            this.max = max;
            count = 0;
            sb = new StringBuilder();
        }

        public void AddLine(string line) {
            lock(this) {
                sb.AppendLine(line);
            }
        }

        public void AddResult(string result) {
            lock(this) {
                if(count < max) {
                    if(count > 0) {
                        sb.AppendLine();
                    }
                    result = count++ + 1 + ": " + result;
                    sb.AppendLine(result);
                }
            }
        }

        public void Print() {
            Console.WriteLine(sb.ToString());
        }
    }

    /*
    Helper class for validating command-line arguments
    */
    public static class ArgumentHelper {
        
        public static readonly string HELP_MESSAGE = 
        @"Usage: dotnet run <bits> <prime|odd> <count>
        - bits       the number of bits of the number to begenerated, this must be a multiple of 8, and at least 32 bits.
        - option     'odd' or'prime' (the type of numbers to be generated)
        - count      the count of numbers to generate, defaults to 1";

        private static void ErrorOut(string message) {
            Console.WriteLine($"{message}\n{HELP_MESSAGE}");
            Environment.Exit(0);
        }

        public static void ValidateArgumentList(string[] args) {
            if(args.Count() != 2 && args.Count() != 3) {
                ErrorOut($"Actual ({args.Count()}) and required (2|3) argument lists differ in length.");
            }
        }


        public static int ValidateBits(string arg) {
            int bits;
            if(!int.TryParse(arg, out bits)) {
                ErrorOut($"Provided value for bits ({arg}) is not an integer.");
            }
            if(bits % 8 != 0) {
                ErrorOut($"Provided value for bits ({arg}) is not a multiple of 8.");
            }
            if(bits < 32) {
                ErrorOut($"Provided value for bits ({arg}) is not at least 32.");
            }
            return bits;
        }

        public static string ValidateOption(string arg) {
            if(arg.ToLowerInvariant() != "prime" && arg.ToLowerInvariant() != "odd") {
                ErrorOut($"Provided value for option ({arg}) is not one of \"prime\" or \"odd\".");
            }
            return arg;
        }

        public static int ValidateCount(string? arg) {
            if(arg == null) {
                return 1;
            }

            int count;
            if(!int.TryParse(arg, out count)) {
                ErrorOut($"Provided value for count ({arg}) is not an integer.");
            }
            if(count < 1) {
                ErrorOut($"Provided value for count ({arg}) is not at least 1.");
            }
            return count;
        }
    }

    /*
    Main class for program logic
    */
    public static class NumGen {

        public static int BITS = 32;
        public static int COUNT = 1;
        
        public static StringBuilderWrapper buffer;

        public static void Main(string[] args) {
            ArgumentHelper.ValidateArgumentList(args);
            BITS = ArgumentHelper.ValidateBits(args[0]);
            string option = ArgumentHelper.ValidateOption(args[1]);
            COUNT = ArgumentHelper.ValidateCount(args.Count() > 2 ? args[2] : null);

            buffer = new StringBuilderWrapper(COUNT); 
            buffer.AddLine("BitLength: " + BITS);

            TimeSpan ts;
            if(option == "odd") {
                ts = FactorOddNumbers();
            } else {
                ts = GeneratePrimes();
            }

            string elapsedTime = string.Format(
                "{0:00}:{1:00}:{2:00}.{3:000000}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Microseconds
            );

            buffer.AddLine($"Time to Generate: {elapsedTime}");
            buffer.Print();
        }

        public static TimeSpan GeneratePrimes() {

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for(int i = 0; i < COUNT; i++) {
                ThreadPool.QueueUserWorkItem(FindPrime);
            }

            while(ThreadPool.CompletedWorkItemCount < COUNT);

            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        //WaitCallback Delegate for ThreadPool
        public static void FindPrime(object? stateInfo) {
            BigInteger num = BigIntegerExtensions.NextPositiveBigInteger(BITS / 8);
            while(!num.isProbablyPrime()) {
                num = BigIntegerExtensions.NextPositiveBigInteger(BITS / 8);
            }
            buffer.AddResult(num.ToString());
        }

        public static TimeSpan FactorOddNumbers() {

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for(int i = 0; i < COUNT; i++) {
                BigInteger odd = BigIntegerExtensions.NextPositiveBigInteger(BITS / 8);
                int factors = CountFactors(odd);
                string result = $"{odd}\nNumber of factors: {factors}";
                buffer.AddResult(result);
            };

            
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        public static int CountFactors(BigInteger n) {
            int numFactors = 0;
            for (int i = 1; i <= n.Sqrt(); i++) { 
                if (n % i == 0) { 
                    if (n / i == i) 
                        numFactors++;
                    else 
                        numFactors += 2;
                } 
            } 
            return numFactors;
        }

    } 

}
