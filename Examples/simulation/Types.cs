namespace Tui.Core
{
    // -------------------------------------------------------------------------
    // Color
    // 16-color terminal palette mapping to System.ConsoleColor.
    // -------------------------------------------------------------------------
    public enum Color
    {
        Black       = 0,
        DarkRed     = 1,
        DarkGreen   = 2,
        DarkYellow  = 3,
        DarkBlue    = 4,
        DarkMagenta = 5,
        DarkCyan    = 6,
        Gray        = 7,
        DarkGray    = 8,
        Red         = 9,
        Green       = 10,
        Yellow      = 11,
        Blue        = 12,
        Magenta     = 13,
        Cyan        = 14,
        White       = 15,
    }

    // -------------------------------------------------------------------------
    // Cell
    // A single rendered terminal character with foreground and background color.
    // -------------------------------------------------------------------------
    public struct Cell
    {
        public char  Char;
        public Color Foreground;
        public Color Background;

        public Cell(char ch, Color fg, Color bg)
        {
            Char       = ch;
            Foreground = fg;
            Background = bg;
        }

        public bool Equals(Cell other)
        {
            return Char == other.Char
                && Foreground == other.Foreground
                && Background == other.Background;
        }

        public override bool Equals(object obj)
        {
            if (obj is Cell)
            {
                Cell c = (Cell)obj;
                return Equals(c);
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + Char.GetHashCode();
            hash = hash * 31 + Foreground.GetHashCode();
            hash = hash * 31 + Background.GetHashCode();
            return hash;
        }

        public static bool operator ==(Cell a, Cell b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Cell a, Cell b)
        {
            return !a.Equals(b);
        }
    }

    // -------------------------------------------------------------------------
    // Rect
    // Integer rectangle described by position and size.
    // -------------------------------------------------------------------------
    public struct Rect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Rect(int x, int y, int w, int h)
        {
            X      = x;
            Y      = y;
            Width  = w;
            Height = h;
        }

        public int Right
        {
            get { return X + Width; }
        }

        public int Bottom
        {
            get { return Y + Height; }
        }

        public bool Contains(int x, int y)
        {
            return x >= X && x < Right && y >= Y && y < Bottom;
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + " " + Width + "x" + Height + ")";
        }
    }
}
