namespace Extensions {
    using System.Numerics;

    public static class SMExtensions {

        public static BigInteger modInverse(BigInteger a, BigInteger b) {
            BigInteger i = b, v = 0, d = 1;
            while (a>0) {
                BigInteger z = i/a, x = a;
                a = i % x;
                i = x;
                x = d;
                d = v - z*x;
                v = x;
            }
            v %= b;
            if (v<0) v = (v+b) % b;
            return v;
        }
    }

}

namespace SecureMessaging {

    public static class SecureMessaging {

        private static readonly string HELP_MESSAGE = 
            @"Usage: dotnet run <option> <other arguments>
              options:
              keyGen <keysize>              - Generate a keypair of keysize bits
              sendKey <email>               - Send the key to the specified email
              getKey <email>                - Get the public key from the specified email
              sendMsg <email> <plaintext>   - Send a specified plaintext message to a specified email
              getMsg <email>                - Retrieve a message from a specified email";

        
        private static readonly List<string> VALID_OPTIONS;
        private static readonly Dictionary<string, SMAction> ACTIONS;

        public delegate void SMAction(string[] param);

        static SecureMessaging() {
            ACTIONS = new Dictionary<string, SMAction>();
            ACTIONS.Add("keyGen", keyGen);
            ACTIONS.Add("sendKey", sendKey);
            ACTIONS.Add("getKey", getKey);
            ACTIONS.Add("sendMsg", sendMsg);
            ACTIONS.Add("getMsg", getMsg);

            VALID_OPTIONS = ACTIONS.Keys.ToList();
        }

        public static void Main(string[] args) {
            VerifyArgumentAction(args);

        }

        private static void VerifyArgumentAction(string[] args) {
            if(args.Length < 1) {
                Console.WriteLine("No options provided\n" + HELP_MESSAGE);
                Environment.Exit(0);
            } else if(!VALID_OPTIONS.Contains(args[0])) {
                Console.WriteLine("Provided option is not valid\n" + HELP_MESSAGE);
                Environment.Exit(0);
            }
        }

        private static void DispatchAction(string[] args) {
            string action = args[0];
            string[] param = (string[]) args.Skip(1);
            ACTIONS.GetValueOrDefault(action)?.Invoke(param);
        }

        private static void keyGen(string[] param) {

        }

        private static void sendKey(string[] param) {

        }

        private static void getKey(string[] param) {

        }

        private static void sendMsg(string[] param) {

        }

        private static void getMsg(string[] param) {

        }

    }

}
