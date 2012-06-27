using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Console {
    class Program {
        static void Main(string[] args) {

            using (var redisClient = new ServiceStack.Redis.RedisClient("127.0.0.1", 6379)) {
                redisClient.Add<string>("session", "chadmeyer", DateTime.Now.AddMinutes(1));

                System.Console.WriteLine(string.Format("value of session is {0}", redisClient.Get<string>("session")));

                System.Threading.Thread.Sleep(2000);

                redisClient.Add("timer", "1", DateTime.Now.AddSeconds(15));

                System.Console.WriteLine("added a timer");

                bool keyExists = true;
                int count = 1;

                while (keyExists) {
                    if (redisClient.ContainsKey("timer")) {
                        System.Console.WriteLine(string.Format("counting the timer to expired key {0}", count));
                        count++;
                        System.Threading.Thread.Sleep(1000);
                    }
                    else {
                        keyExists = false;
                        System.Console.WriteLine(string.Format("Key was expired after {0} seconds", count));
                    }
                }
            }


            System.Console.ReadLine();

        }
    }
}
