using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

public abstract class TileSpec
{
    private static List<TileSpec> allSpecs = new List<TileSpec>();
    public abstract RectF GetTileSprite(TileSpec top, TileSpec right, TileSpec down, TileSpec left);

    public static TileSpec AddSpec(TileSpec spec)
    {
        if(allSpecs.Contains(spec))
            throw new Exception("adding tile spec twice");
        allSpecs.Add(spec);
        return spec;
    }

    public static TileSpec GetAt(int i = 0)
    {
        return allSpecs[i];
    }

}

public class SingleSpriteTileSpec : TileSpec
{
    private RectF sprite;
    public SingleSpriteTileSpec(RectF sprite)
    {
        this.sprite = sprite;
    }
    public override RectF GetTileSprite(TileSpec top, TileSpec right, TileSpec down, TileSpec left)
    {
        return sprite;
    }
}

public class WeldableTileSpec : TileSpec
{
    public struct WeldableTileSpriteSpec
    {
        public RectF rect;
        public NeighborsState ns;
        public WeldableTileSpriteSpec(bool top, bool right, bool down, bool left, RectF rect)
        {
            ns.top = top;
            ns.right = right;
            ns.down = down;
            ns.left = left;
            this.rect = rect;
        }
    }

    //top right down left
    public WeldableTileSpriteSpec m0000;
    public WeldableTileSpriteSpec m1111;
    public WeldableTileSpriteSpec m1010;
    public WeldableTileSpriteSpec m0101;
    public WeldableTileSpriteSpec m1000;
    public WeldableTileSpriteSpec m0100;
    public WeldableTileSpriteSpec m0010;
    public WeldableTileSpriteSpec m0001;
    public WeldableTileSpriteSpec m1100;
    public WeldableTileSpriteSpec m0110;
    public WeldableTileSpriteSpec m0011;
    public WeldableTileSpriteSpec m1001;
    public WeldableTileSpriteSpec m0111;
    public WeldableTileSpriteSpec m1011;
    public WeldableTileSpriteSpec m1101;
    public WeldableTileSpriteSpec m1110;
    public WeldableTileSpriteSpec[] specs;
        
    public override RectF GetTileSprite(TileSpec top, TileSpec right, TileSpec down, TileSpec left)
    {
        NeighborsState ns = new NeighborsState();

        if (IsThis(top)) ns.top = true;
        if (IsThis(right)) ns.right = true;
        if (IsThis(down)) ns.down = true;
        if (IsThis(left)) ns.left = true;

        foreach (WeldableTileSpriteSpec spec in specs)
        {
            if (spec.ns == ns)
            {
                return spec.rect;
            }
        }
        return m0000.rect;
    }

    public bool IsThis(TileSpec other)
    {
        if (other == null) return false;
        if (other == this)
            return true;
        return false;
    }

}

public class WallSpec : WeldableTileSpec
{
    //left, right
    public SingleSpriteTileSpec b01;
    public SingleSpriteTileSpec b11;
    public SingleSpriteTileSpec b10;
    public SingleSpriteTileSpec b00;
        
    public WallSpec(RectF wallRect)
    {
        m0000 = new WeldableTileSpriteSpec(false,false,false,false,wallRect.GetSubTile(new Point(3,3), 5));
        m1111 = new WeldableTileSpriteSpec(true,true,true,true,    wallRect.GetSubTile(new Point(1,1), 5));
        m1010 = new WeldableTileSpriteSpec(true,false,true,false,  wallRect.GetSubTile(new Point(3,1), 5));
        m0101 = new WeldableTileSpriteSpec(false,true,false,true,  wallRect.GetSubTile(new Point(1,3), 5));
        m1000 = new WeldableTileSpriteSpec(true,false,false,false, wallRect.GetSubTile(new Point(3,2), 5));
        m0100 = new WeldableTileSpriteSpec(false,true,false,false, wallRect.GetSubTile(new Point(0,3), 5));
        m0010 = new WeldableTileSpriteSpec(false,false,true,false, wallRect.GetSubTile(new Point(3,0), 5));
        m0001 = new WeldableTileSpriteSpec(false,false,false,true, wallRect.GetSubTile(new Point(2,3), 5));
        m1100 = new WeldableTileSpriteSpec(true,true,false,false,  wallRect.GetSubTile(new Point(0,2), 5));
        m0110 = new WeldableTileSpriteSpec(false,true,true,false,  wallRect.GetSubTile(new Point(0,0), 5));
        m0011 = new WeldableTileSpriteSpec(false,false,true,true,  wallRect.GetSubTile(new Point(2,0), 5));
        m1001 = new WeldableTileSpriteSpec(true,false,false,true,  wallRect.GetSubTile(new Point(2,2), 5));
        m0111 = new WeldableTileSpriteSpec(false,true,true,true,   wallRect.GetSubTile(new Point(1,0), 5));
        m1011 = new WeldableTileSpriteSpec(true,false,true,true,   wallRect.GetSubTile(new Point(2,1), 5));
        m1101 = new WeldableTileSpriteSpec(true,true,false,true,   wallRect.GetSubTile(new Point(1,2), 5));
        m1110 = new WeldableTileSpriteSpec(true,true,true,false,   wallRect.GetSubTile(new Point(0,1), 5));
        specs = new []{m0000,m1111,m1010,m0101,m1000,m0100,m0010,m0001,m1100,m0110,m0011,m1001,m0111,m1011,m1101,m1110};

        b01 = new SingleSpriteTileSpec(wallRect.GetSubTile(new Point(0,4), 5));
        b11 = new SingleSpriteTileSpec(wallRect.GetSubTile(new Point(1,4), 5));
        b10 = new SingleSpriteTileSpec(wallRect.GetSubTile(new Point(2,4), 5));
        b00 = new SingleSpriteTileSpec(wallRect.GetSubTile(new Point(3,4), 5));

    }

    public TileSpec GetWallBottom(TileSpec left, TileSpec right)
    {
        bool l = IsThis(left);
        bool r = IsThis(right);
        if(l && r) return b11;
        if(l) return b10;
        if(r) return b01;
        return b00;
    }
        
}