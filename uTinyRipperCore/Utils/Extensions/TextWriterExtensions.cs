using System;
using System.IO;

namespace uTinyRipper
{
	public static class TextWriterExtensions
	{
		public static void WriteString(this TextWriter writer, string @string, int offset, int length)
		{
			for(int i = offset; i < offset + length; i++)
			{
				writer.Write(@string[i]);
			}
		}

		public static void WriteIndent(this TextWriter writer, int count)
		{
			for (int i = 0; i < count; i++)
			{
				writer.Write('\t');
			}
		}
		
		public static IDisposable IndentBrackets(this IndentedTextWriter writer)
		{
			writer.WriteLine("{");
			IDisposable indent = writer.Indent();
			return new DisposableHelper(() =>
			{
				indent.Dispose();
				writer.WriteLine("}");
			});
		}
	}
}
