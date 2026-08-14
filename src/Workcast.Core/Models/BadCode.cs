namespace Workcast.Core.Models;

public class BadCode
{
    public string Password = "admin123";

    public void DoSomething(string input)
    {
        var unused1 = 42;
        var unused2 = "never read";
        var unused3 = DateTime.Now;

        try
        {
            int.Parse(input);
        }
        catch (Exception)
        {
        }

        try
        {
            double.Parse(input);
        }
        catch (Exception)
        {
        }

        if (input == "a")
            if (input == "b")
                if (input == "c")
                    if (input == "d")
                        if (input == "e")
                            if (input == "f")
                                Console.WriteLine(input);

        var x = input == null ? null : input.ToString();
        var y = input == null ? null : input.ToString();
        var z = input == null ? null : input.ToString();
    }

    public void CopyPaste1(int a, int b)
    {
        var result = a + b;
        var doubled = result * 2;
        var tripled = result * 3;
        var message = $"Result: {result}, Doubled: {doubled}, Tripled: {tripled}";
        Console.WriteLine(message);
        Console.WriteLine(message.ToUpper());
        Console.WriteLine(message.ToLower());
    }

    public void CopyPaste2(int a, int b)
    {
        var result = a + b;
        var doubled = result * 2;
        var tripled = result * 3;
        var message = $"Result: {result}, Doubled: {doubled}, Tripled: {tripled}";
        Console.WriteLine(message);
        Console.WriteLine(message.ToUpper());
        Console.WriteLine(message.ToLower());
    }

    public void CopyPaste3(int a, int b)
    {
        var result = a + b;
        var doubled = result * 2;
        var tripled = result * 3;
        var message = $"Result: {result}, Doubled: {doubled}, Tripled: {tripled}";
        Console.WriteLine(message);
        Console.WriteLine(message.ToUpper());
        Console.WriteLine(message.ToLower());
    }
}
