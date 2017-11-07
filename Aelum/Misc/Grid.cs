using System.Collections.Generic;
using MessagePack;
using Microsoft.Xna.Framework;

[MessagePackObject]
public class Grid<T>
{
    [Key(0)]
    private readonly T[,] cells_;

    public Grid(T[,] cells)
    {
        cells_ = cells;
    }

    public Grid(int w, int h, T def = default(T))
    {
        cells_ = new T[w,h];
        for (int y = 0; y <= cells_.GetUpperBound(1); y++)
        for (int x = 0; x <= cells_.GetUpperBound(0); x++)
        {
            cells_[x, y] = def;
        }
    }

    public T GetElement(Point pos)
    {
        if (IsOutOfBounds(pos))
            return default(T);
        return cells_[pos.X, pos.Y];
    }

    public T GetElementClamped(Point pos)
    {
        return cells_[
            MathUtils.ClampInt(pos.X,0,cells_.GetUpperBound(0)),
            MathUtils.ClampInt(pos.Y,0,cells_.GetUpperBound(1))
        ];
    }

    public void SetElement(Point pos, T element)
    {
        if (IsOutOfBounds(pos)) return;
        cells_[pos.X, pos.Y] = element;
    }

    public void SetElementClamped(Point pos, T element)
    {
        cells_[
            MathUtils.ClampInt(pos.X, 0, cells_.GetUpperBound(0)),
            MathUtils.ClampInt(pos.Y, 0, cells_.GetUpperBound(1))
        ] = element;
    }

    private bool IsOutOfBounds(Point pos)
    {
        return IsOutOfBounds(pos.X, pos.Y);
    }

    private bool IsOutOfBounds(int x, int y)
    {
        return x < 0 || x > cells_.GetUpperBound(0) || y < 0 || y > cells_.GetUpperBound(1);
    }

    Rectangle GetBoundsRect()
    {
        return new Rectangle(0,0,cells_.GetUpperBound(0),cells_.GetUpperBound(1));
    }

    public IEnumerable<KeyValuePair<Point, T>> LoopAll()
    {
        for (int y = 0; y <= cells_.GetUpperBound(1); y++)
        for (int x = 0; x <= cells_.GetUpperBound(0); x++)
        {
            yield return new KeyValuePair<Point, T>(new Point(x,y), cells_[x,y]);
        }
    }

    public IEnumerable<KeyValuePair<Point, T>> LoopRect(Rectangle rect)
    {
        rect = Rectangle.Intersect(GetBoundsRect(), rect);
        foreach (Point pos in rect.LoopArea())
        {
            yield return new KeyValuePair<Point, T>(pos, cells_[pos.X, pos.Y]);
        }
    }

    public IEnumerable<KeyValuePair<Point, T>> LoopRectBoundsClip(Rectangle rect)
    {
        foreach (Point p in rect.LoopBoundary())
        {
            if (!IsOutOfBounds(p))
            {
                yield return new KeyValuePair<Point, T>(p,  cells_[p.X, p.Y]);
            }
        }
    }

    public void SetAll(T val)
    {
        for (int y = 0; y <= cells_.GetUpperBound(1); y++)
        for (int x = 0; x <= cells_.GetUpperBound(0); x++)
        {
            cells_[x,y] = val;
        }
    }
    
    public void SetAllInArea(Rectangle rect, T val)
    {
        rect = Rectangle.Intersect(GetBoundsRect(), rect);
        foreach (Point pos in rect.LoopArea())
        {
            cells_[pos.X, pos.Y] = val;
        }
    }

    public void SetAllInBoundsClip(Rectangle rect, T val)
    {
        foreach (Point p in rect.LoopBoundary())
        {
            if (!IsOutOfBounds(p))
            {
                cells_[p.X, p.Y] = val;
            }
        }
    }


}