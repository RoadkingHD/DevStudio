using System;

namespace HelloCSharp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            int sum = Sum(2, 3);
            Console.WriteLine("Hello from DevStudio C#! 2 + 3 = " + sum);
            Console.ReadLine();
        }

        public static int Sum(int a, int b)
        {
            return a + b;
        }
    }
}
