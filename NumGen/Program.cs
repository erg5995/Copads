﻿

using System.Numerics;

namespace Extensions {

    using NumGen;

    public static class BigIntegerExtensions {
        //https://stackoverflow.com/questions/3432412/calculate-square-root-of-a-biginteger-system-numerics-biginteger
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

            throw new ArithmeticException("NaN");
        }

        private static bool isSqrt(BigInteger n, BigInteger root)
        {
            BigInteger lowerBound = root*root;
            return n >= lowerBound && n <= lowerBound + root + root;
        }

        //https://stackoverflow.com/a/68593532/13471744
        public static BigInteger NextBigInteger(this Random random, BigInteger minValue, BigInteger maxValue) {
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

        public static BigInteger NextBigInteger(this Random random, int numBytes) {
            byte[] bytes = new byte[numBytes];
            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(bytes);
            return new BigInteger(bytes);
        }

        public static bool isProbablyPrime(this BigInteger num) {
            int k = 4;
            if ((num < 2) || (num % 2 == 0)) return (num == 2);

            BigInteger s = BigInteger.Subtract(num, BigInteger.One);
            while (s % 2 == 0) s >>= 1; //Factor powers of 2 from n - 1

            Random r = new Random();
            for (int i = 0; i < k; i++)
            {
                BigInteger a = r.NextBigInteger(2, num - 1);
                BigInteger x = BigInteger.ModPow(a, s, num);
                BigInteger y = BigInteger.One;
                for(int n = 0; n < s; n++) {
                    y = BigInteger.ModPow(x, 2, n);
                    if(y == 1 && x != 1 && x != n - 1) {
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
    using Microsoft.VisualBasic;

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

    public static class NumGen {

        public static int BITS = 32;
        public static int COUNT = 1;
        
        public static readonly StringBuilder PRINT_BUFFER = new StringBuilder();

        public static void Main(string[] args) {
            ArgumentHelper.ValidateArgumentList(args);
            BITS = ArgumentHelper.ValidateBits(args[0]);
            string option = ArgumentHelper.ValidateOption(args[1]);
            COUNT = ArgumentHelper.ValidateCount(args.Count() > 2 ? args[2] : null);

            PRINT_BUFFER.AppendLine("BitLength: " + BITS);

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

            PRINT_BUFFER.AppendLine($"Time to Generate: {elapsedTime}");
            Console.WriteLine(PRINT_BUFFER);
        }

        public static TimeSpan GeneratePrimes() {
            return new TimeSpan();
        }

        public static TimeSpan FactorOddNumbers() {
            Random random = new Random();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for(int i = 0; i < COUNT; i++) {
                BigInteger odd = random.NextBigInteger(BITS / 8);
                PRINT_BUFFER.AppendLine($"{i}: {odd.ToString()}");
                int factors = CountFactors(odd);
                PRINT_BUFFER.AppendLine($"Number of factors: {factors}");
            }
            
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
