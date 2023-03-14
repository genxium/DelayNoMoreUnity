C# has the option to use `unsafe pointer`, e.g. when we have a 
```csharp
class Foo { ... }
```

it's possible to use it by the pointer type like
```
Foo f1;
Foo* f1Ptr = &f1;
```

, which is quite concerning to me because this is not conventional for a guy coming from Java experience. I have to verify that if I want to **change the value of `a field of a class instance`**`, does the Java way still apply? Do I have to use unsafe pointers in C# for it?  

My testing snippet is as follows, fortunately the Java way works for C#, without any need for unsafe pointer. It's runnable online at https://dotnetfiddle.net/.
```
using System;
					
public class Foo {
  public int a;
}

public class Program
{
	public static void changeValue1(int newVal, Foo f) {
		f.a = newVal;
	}
	
	public static void changeValue2(int newVal, Foo f, out Foo holder) {
		holder = f;
		holder.a = newVal;
	}
	
	public static void Main()
	{
		Foo f1 = new Foo();
		f1.a = 4;
		changeValue1(41, f1);
		Console.WriteLine("{0}", f1.a);
		Foo holder;
		changeValue2(54, f1, out holder);
		Console.WriteLine("{0}", f1.a);
	}
}
```

It's also worth noticing that C# by default allocates all arrays in heap, if not otherwise specified by [stackalloc](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc). 

Moreover, [an official guide says that it's recommended to use `struct` instead of `class` whenever possible because the former can be allocated on stack](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/#improve-performance-with-ref-safety). 
