```cs
    bool one = true;
    bool three = true;
    if (one ^ three )
    {
        Console.WriteLine(true ^ true);    // output: False
        Console.WriteLine(true ^ false);   // output: True
        Console.WriteLine(false ^ true);   // output: True
        Console.WriteLine(false ^ false);  // output: False
    }
```
