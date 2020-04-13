using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace PerfTest
{
	class Program
	{
		static void Main(string[] args)
		{
			BenchmarkRunner.Run<TextWriterBenchmark>();
		}
	}


	public class TextWriterBenchmark
	{
		private string line;
		private TextWriter writer;

		[ParamsSource(nameof(WriterValues))] public Box<Func<TextWriter>> Writer { get; set; }

		public Box<Func<TextWriter>>[] WriterValues => new Box<Func<TextWriter>>[]
		{
			new Func<TextWriter>(() => new StringWriter()).Tag("StringWriter"),
			new Func<TextWriter>(() => new StreamWriter(new MemoryStream())).Tag("SW/MS"),
			new Func<TextWriter>(() => new StreamWriter(Stream.Null)).Tag("SW/NS"),
			new Func<TextWriter>(() => new StreamWriter("C:\\dir\\" + Guid.NewGuid().ToString("N"))).Tag("SW/ssd"),
			new Func<TextWriter>(() => new StreamWriter("D:\\dir\\" + Guid.NewGuid().ToString("N"))).Tag("SW/hdd"),
			new Func<TextWriter>(() => new System.CodeDom.Compiler.IndentedTextWriter(new StringWriter())).Tag("CDIW/StringWriter"),
			new Func<TextWriter>(() => new System.CodeDom.Compiler.IndentedTextWriter(new StreamWriter(new MemoryStream()))).Tag("CDIW/SW/MS"),
			new Func<TextWriter>(() => new System.CodeDom.Compiler.IndentedTextWriter(new StreamWriter(Stream.Null))).Tag("CDIW/SW/NS"),
			new Func<TextWriter>(() => new System.CodeDom.Compiler.IndentedTextWriter(new StreamWriter("C:\\dir\\" + Guid.NewGuid().ToString("N")))).Tag("CDIW/SW/ssd"),
			new Func<TextWriter>(() => new System.CodeDom.Compiler.IndentedTextWriter(new StreamWriter("D:\\dir\\" + Guid.NewGuid().ToString("N")))).Tag("CDIW/SW/hdd"),
			new Func<TextWriter>(() => new IndentedTextWriter(new StringWriter())).Tag("IW/StringWriter"),
			new Func<TextWriter>(() => new IndentedTextWriter(new StreamWriter(new MemoryStream()))).Tag("IW/SW/MS"),
			new Func<TextWriter>(() => new IndentedTextWriter(new StreamWriter(Stream.Null))).Tag("IW/SW/NS"),
			new Func<TextWriter>(() => new IndentedTextWriter(new StreamWriter("C:\\dir\\" + Guid.NewGuid().ToString("N")))).Tag("IW/SW/ssd"),
			new Func<TextWriter>(() => new IndentedTextWriter(new StreamWriter("D:\\dir\\" + Guid.NewGuid().ToString("N")))).Tag("IW/SW/hdd"),
			new Func<TextWriter>(() => new IndentedTextWriter2(new StringWriter())).Tag("IW2/StringWriter"),
			new Func<TextWriter>(() => new IndentedTextWriter2(new StreamWriter(new MemoryStream()))).Tag("IW2/SW/MS"),
			new Func<TextWriter>(() => new IndentedTextWriter2(new StreamWriter(Stream.Null))).Tag("IW2/SW/NS"),
			new Func<TextWriter>(() => new IndentedTextWriter2(new StreamWriter("C:\\dir\\" + Guid.NewGuid().ToString("N")))).Tag("IW2/SW/ssd"),
			new Func<TextWriter>(() => new IndentedTextWriter2(new StreamWriter("D:\\dir\\" + Guid.NewGuid().ToString("N")))).Tag("IW2/SW/hdd"),
		};

		[Params(/*100, 1000, 10000,*/ 100000)] public int NumWrites { get; set; }
		[Params(/*10, 100,*/ 1000)] public int LineSize { get; set; }

		[GlobalSetup]
		public void Setup()
		{
			line = new string('s', LineSize);
		}

		[IterationSetup]
		public void SetupIter()
		{
			writer = Writer.Value();
		}

		[Benchmark]
		public void WriteLineSimple()
		{
			var w = writer;

			for (int i = 0; i < NumWrites; i++)
			{
				w.WriteLine(line);
			}
		}
	}

	public interface Box<out T>
	{
		T Value { get; }
	}

	public class Tagged<T> : Box<T>
	{
		public Tagged(T value, string tag)
		{
			Value = value;
			Tag = tag;
		}

		public T Value { get; }
		public string Tag { get; }

		public override string ToString()
		{
			return Tag;
		}
	}

	public static class Extension
	{
		public static Box<T> Tag<T>(this T self, string tag)
		{
			return new Tagged<T>(self, tag);
		}
	}

	/// <summary>
	/// A TextWriter that automatically indents text
	/// Additionally it will unify <c>\r</c> , <c>\n</c> and <see cref="TextWriter.NewLine"/>
	/// </summary>
	public class IndentedTextWriter : TextWriter
	{
		public IndentedTextWriter(TextWriter inner, string indent = "\t")
		{
			m_inner = inner ?? throw new ArgumentNullException(nameof(inner));
			m_indent = indent ?? throw new ArgumentNullException(nameof(indent));
			base.NewLine = m_inner.NewLine;
		}

		//public IDisposable Indent()
		//{
		//	m_indentLevel++;
		//	return new DisposableHelper(() => m_indentLevel--);
		//}

		//public IDisposable DisableIndent(bool indentCurrent = false)
		//{
		//	if (indentCurrent)
		//	{
		//		MaybeWriteIndent();
		//	}

		//	m_disableIndent++;
		//	return new DisposableHelper(() => m_disableIndent--);
		//}

		public void MaybeWriteIndent()
		{
			if (m_indentPending)
			{
				m_indentPending = false;

				if (m_disableIndent == 0)
				{
					for (int i = 0; i < m_indentLevel; i++)
					{
						Write(m_indent);
					}
				}
			}
		}

		public override void WriteLine()
		{
			m_inner.WriteLine();
			m_indentPending = true;
		}

		public override void Write(char value)
		{
			if (CoreNewLine[m_newLineState] == value)
			{
				m_newLineState++;
			}
			else if (m_newLineState != 0)
			{
				if (CoreNewLine[m_newLineState - 1] == '\r' || CoreNewLine[m_newLineState - 1] == '\n')
				{
					m_inner.Write(CoreNewLine, 0, m_newLineState - 1);
					WriteLine();
				}
				else
				{
					m_inner.Write(CoreNewLine, 0, m_newLineState);
				}

				m_newLineState = 0;
			}

			if (m_newLineState == CoreNewLine.Length)
			{
				WriteLine();
				m_newLineState = 0;
			}
			else if (m_newLineState == 0)
			{
				if (value == '\r' || value == '\n')
				{
					WriteLine();
				}
				else
				{
					MaybeWriteIndent();
					m_inner.Write(value);
				}
			}
		}

		public override Encoding Encoding => m_inner.Encoding;

		public override IFormatProvider FormatProvider => m_inner.FormatProvider;

		protected override void Dispose(bool disposing)
		{
			m_inner.Dispose();
			base.Dispose(disposing);
		}

		private readonly TextWriter m_inner;
		private readonly string m_indent;
		private int m_disableIndent;
		private int m_indentLevel;
		private int m_newLineState;
		private bool m_indentPending;
	}

	public class IndentedTextWriter2 : TextWriter
	{
		public IndentedTextWriter2(TextWriter inner, string indent = "\t")
		{
			m_inner = inner ?? throw new ArgumentNullException(nameof(inner));
			m_indent = indent ?? throw new ArgumentNullException(nameof(indent));
			base.NewLine = m_inner.NewLine;
		}

		//public IDisposable Indent()
		//{
		//	m_indentLevel++;
		//	return new DisposableHelper(() => m_indentLevel--);
		//}

		//public IDisposable DisableIndent(bool indentCurrent = false)
		//{
		//	if (indentCurrent)
		//	{
		//		MaybeWriteIndent();
		//	}

		//	m_disableIndent++;
		//	return new DisposableHelper(() => m_disableIndent--);
		//}
		

		private void MaybeIndent()
		{
			if (m_indentPending)
			{
				m_indentPending = false;
				if (m_disableIndent == 0)
				{
					for (int i = 0; i < m_indentLevel; i++)
					{
						m_inner.Write(m_indent);
					}
				}
			}
		}

		private async Task MaybeIndentAsync()
		{
			if (m_indentPending)
			{
				m_indentPending = false;
				if (m_disableIndent == 0)
				{
					for (int i = 0; i < m_indentLevel; i++)
					{
						await m_inner.WriteAsync(m_indent);
					}
				}
			}
		}

		/// <inheritdoc />
		public override void Flush()
		{
			m_inner.Flush();
		}

		/// <inheritdoc />
		public override async Task FlushAsync()
		{
			await m_inner.FlushAsync();
		}

		/// <inheritdoc />
		public override void Write(bool value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(char value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(char[] buffer)
		{
			MaybeIndent();
			m_inner.Write(buffer);
		}

		/// <inheritdoc />
		public override void Write(char[] buffer, int index, int count)
		{
			MaybeIndent();
			m_inner.Write(buffer, index, count);
		}

		/// <inheritdoc />
		public override void Write(decimal value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(double value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(int value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(long value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(object value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(float value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(string value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(string format, object arg0)
		{
			MaybeIndent();
			m_inner.Write(format, arg0);
		}

		/// <inheritdoc />
		public override void Write(string format, object arg0, object arg1)
		{
			MaybeIndent();
			m_inner.Write(format, arg0, arg1);
		}

		/// <inheritdoc />
		public override void Write(string format, object arg0, object arg1, object arg2)
		{
			MaybeIndent();
			m_inner.Write(format, arg0, arg1, arg2);
		}

		/// <inheritdoc />
		public override void Write(string format, params object[] arg)
		{
			MaybeIndent();
			m_inner.Write(format, arg);
		}

		/// <inheritdoc />
		public override void Write(uint value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override void Write(ulong value)
		{
			MaybeIndent();
			m_inner.Write(value);
		}

		/// <inheritdoc />
		public override async Task WriteAsync(char value)
		{
			await MaybeIndentAsync();
			await m_inner.WriteAsync(value);
		}

		/// <inheritdoc />
		public override async Task WriteAsync(char[] buffer, int index, int count)
		{
			await MaybeIndentAsync();
			await m_inner.WriteAsync(buffer, index, count);
		}

		/// <inheritdoc />
		public override async Task WriteAsync(string value)
		{
			await MaybeIndentAsync();
			await m_inner.WriteAsync(value);
		}

		/// <inheritdoc />
		public override void WriteLine()
		{
			m_inner.WriteLine();
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(bool value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(char value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(char[] buffer)
		{
			MaybeIndent();
			m_inner.WriteLine(buffer);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(char[] buffer, int index, int count)
		{
			MaybeIndent();
			m_inner.WriteLine(buffer, index, count);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(decimal value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(double value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(int value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(long value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(object value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(float value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(string value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(string format, object arg0)
		{
			MaybeIndent();
			m_inner.WriteLine(format, arg0);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(string format, object arg0, object arg1)
		{
			MaybeIndent();
			m_inner.WriteLine(format, arg0, arg1);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(string format, object arg0, object arg1, object arg2)
		{
			MaybeIndent();
			m_inner.WriteLine(format, arg0, arg1, arg2);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(string format, params object[] arg)
		{
			MaybeIndent();
			m_inner.WriteLine(format, arg);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(uint value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override void WriteLine(ulong value)
		{
			MaybeIndent();
			m_inner.WriteLine(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override async Task WriteLineAsync()
		{
			await m_inner.WriteLineAsync();
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override async Task WriteLineAsync(char value)
		{
			await MaybeIndentAsync();
			await m_inner.WriteLineAsync(value);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override async Task WriteLineAsync(char[] buffer, int index, int count)
		{
			await MaybeIndentAsync();
			await m_inner.WriteLineAsync(buffer, index, count);
			m_indentPending = true;
		}

		/// <inheritdoc />
		public override async Task WriteLineAsync(string value)
		{
			await MaybeIndentAsync();
			await base.WriteLineAsync(value);
			m_indentPending = true;
		}

		public void WriteIndentedFull(string value)
		{
			foreach (char c in value)
			{
				WriteCharWithState(c);
			}
		}

		public void WriteCharWithState(char value)
		{
			if (CoreNewLine[m_newLineState] == value)
			{
				m_newLineState++;
			}
			else if (m_newLineState != 0)
			{
				if (CoreNewLine[m_newLineState - 1] == '\r' || CoreNewLine[m_newLineState - 1] == '\n')
				{
					m_inner.Write(CoreNewLine, 0, m_newLineState - 1);
					WriteLine();
				}
				else
				{
					m_inner.Write(CoreNewLine, 0, m_newLineState);
				}

				m_newLineState = 0;
			}

			if (m_newLineState == CoreNewLine.Length)
			{
				WriteLine();
				m_newLineState = 0;
			}
			else if (m_newLineState == 0)
			{
				if (value == '\r' || value == '\n')
				{
					WriteLine();
				}
				else
				{
					MaybeIndent();
					m_inner.Write(value);
				}
			}
		}

		public override Encoding Encoding => m_inner.Encoding;

		public override IFormatProvider FormatProvider => m_inner.FormatProvider;

		protected override void Dispose(bool disposing)
		{
			m_inner.Dispose();
			base.Dispose(disposing);
		}

		private readonly TextWriter m_inner;
		private readonly string m_indent;
		private int m_disableIndent;
		private int m_indentLevel;
		private int m_newLineState;
		private bool m_indentPending;
	}
}
