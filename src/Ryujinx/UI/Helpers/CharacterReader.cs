using System;

#nullable enable
namespace Ryujinx.Ava.UI.Helpers;

public ref struct CharacterReader(ReadOnlySpan<char> s)
{
  private ReadOnlySpan<char> _s = s;

  public bool End => this._s.IsEmpty;

  public char Peek => this._s[0];

  public int Position { get; private set; }

  public char Take()
  {
    ++this.Position;
    int num = (int) this._s[0];
    this._s = this._s.Slice(1);
    return (char) num;
  }

  public void SkipWhitespace()
  {
    ReadOnlySpan<char> readOnlySpan = this._s.TrimStart();
    this.Position += this._s.Length - readOnlySpan.Length;
    this._s = readOnlySpan;
  }

  public bool TakeIf(char c)
  {
    if ((int) this.Peek != (int) c)
      return false;
    int num = (int) this.Take();
    return true;
  }

  internal bool TakeIf(string s)
  {
    if (!this.TryPeek(s.Length).SequenceEqual<char>(s.AsSpan()))
      return false;
    this._s = this._s.Slice(s.Length);
    this.Position += s.Length;
    return true;
  }

  public bool TakeIf(Func<char, bool> condition)
  {
    if (!condition(this.Peek))
      return false;
    int num = (int) this.Take();
    return true;
  }

  public ReadOnlySpan<char> TakeUntil(char c)
  {
    int num = 0;
    while (num < this._s.Length && (int) this._s[num] != (int) c)
      ++num;
    ReadOnlySpan<char> until = this._s.Slice(0, num);
    this._s = this._s.Slice(num);
    this.Position += num;
    return until;
  }

  public ReadOnlySpan<char> TakeWhile(Func<char, bool> condition)
  {
    int num = 0;
    while (num < this._s.Length && condition(this._s[num]))
      ++num;
    ReadOnlySpan<char> readOnlySpan = this._s.Slice(0, num);
    this._s = this._s.Slice(num);
    this.Position += num;
    return readOnlySpan;
  }

  public ReadOnlySpan<char> TryPeek(int count)
  {
    return this._s.Length < count ? ReadOnlySpan<char>.Empty : this._s.Slice(0, count);
  }

  public ReadOnlySpan<char> PeekWhitespace()
  {
    return this._s.Slice(0, this._s.Length - this._s.TrimStart().Length);
  }

  public void Skip(int count)
  {
    if (this._s.Length < count)
      throw new IndexOutOfRangeException();
    this._s = this._s.Slice(count);
  }
}
