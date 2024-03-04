

namespace NumGen {

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

        public static void Main(string[] args) {
            ArgumentHelper.ValidateArgumentList(args);
            int bits = ArgumentHelper.ValidateBits(args[0]);
            string option = ArgumentHelper.ValidateOption(args[1]);
            int count = ArgumentHelper.ValidateCount(args.Count() > 2 ? args[2] : null);
        }

    } 

}