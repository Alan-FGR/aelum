using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public interface IDrawSprite
{
    void DrawSprite(SpriteBatch batch);
}

public class TileMap<T> : ManagedComponent<TileMap<T>>
{
    public Grid<T> Tiles { get; }

    public TileMap(Entity entity) : base(entity)
    {

    }

    public override ComponentData GetSerialData()
    {
        throw new NotImplementedException();
    }
    //    [SerializationConstructor]
//    public EntityTile(TileSpec spec, Vector2 pos, TileSpec top, TileSpec right, TileSpec down, TileSpec left)
//    {
//        Spec = spec;
//        Dirty = true;
////        this.collider = collider;
//
//        entity = new Entity(pos);
//        entity.persistent = false;
//        entity.shifts = false;
//
//        //TODO use debug sprite?
//        quad = new Quad(entity, new QuadData(spec.GetTileSprite(top, right, down, left), Vector2.Zero, null));
//    }
//
//    public void RefreshTile(TileSpec top, TileSpec right, TileSpec down, TileSpec left)
//    {
//        quad.quadData.atlasTile = spec.GetTileSprite(top, right, down, left);
//        Dirty = false;
//    }

}





public class EntityTile
{
    private Entity entity;
    private Quad quad;
    public List<colliderData> collider;
    private TileSpec spec;

    private EntityTile top_;
    private EntityTile right_;
    private EntityTile down_;
    private EntityTile left_;

    private bool dirty;

    public TileSpec Spec
    {
        get => spec;
        set {
            spec = value;
            MarkDirty();
            top_?.MarkDirty();//TROLOLOLOLOLLLLLLLLLLL
            right_?.MarkDirty();//TROLOLOLOLOLLLLLLLLLLL
            down_?.MarkDirty();//TROLOLOLOLOLLLLLLLLLLL
            left_?.MarkDirty(); //TROLOLOLOLOLLLLLLLLLLL
            }
    }

    public void SetSpec(TileSpec s)
    {
        spec = s;
    }
    
    public EntityTile(Vector2 pos)
    {
        entity = new Entity(pos);
        entity.persistent = false;
        entity.shifts = false;
        quad = new Quad(entity, new QuadData(Sheet.ID.Obj_MISSING_SPRITE, Vector2.Zero));//TODO fix overflow
    }

    public void RefreshTile()
    {
        if (dirty && spec != null)
        {
            quad.quadData.atlasTile = spec.GetTileSprite(top_?.Spec, right_?.Spec, down_?.Spec, left_?.Spec);
        }
        dirty = false;
    }

    public void MarkDirty()
    {
        dirty = true;
    }

    public void SetNeighbors(EntityTile top, EntityTile right, EntityTile down, EntityTile left)
    {
        top_ = top;
        right_ = right;
        down_ = down;
        left_ = left;
    }
    
    public void SetPosition(Vector2 pos)  //origin shifting requires
    {
        if(entity != null) entity.Position = pos;
    }

    public void Finalize()
    {
        entity?.Dispose();
    }
}

/// <summary> tilemap made of individual entities so they can have components (interactive tilemaps) </summary>
public class TileMapOfEntities : ManagedChunkedComponent<TileMapOfEntities>
{
    static TileMapOfEntities()
    {
        CHUNK_SIZE = 32; //this kinda SUCKS :trollface:
        debugColor = Color.Red;
    }

    private readonly Grid<EntityTile> tiles_;

    public static TileMapOfEntities GetTileMapChunkForPosition(Vector2 pos)
    {
        var c = GetChunkEntityListForPos(pos);
        if (c == null) return null; //there's not even a chunk in there
        return c[0] as TileMapOfEntities;
    }

    public static bool TryGetTileAt(Vector2 pos, out EntityTile tile)
    {
        TileMapOfEntities chunk = GetTileMapChunkForPosition(pos);
        if (chunk == null)
        {
            tile=null;
            return false;
        }

        Vector2 relPos = pos - chunk.entity.Position; //get relative position
        Point idx = relPos.Settle();
        tile = chunk.tiles_.GetElementClamped(idx);
        return true;
    }

    public static void TrySetTileAt(Vector2 pos, TileSpec newSpec)
    {
        TileMapOfEntities chunk = GetTileMapChunkForPosition(pos);
        if (chunk == null) return;

        Vector2 relPos = pos - chunk.entity.Position;
        Point relIdx = relPos.Settle().ClampBoth(0,CHUNK_SIZE);

        var curEl = chunk.tiles_.GetElement(relIdx);

        if (curEl == null) return;

        curEl.Spec = newSpec;

        //wall bottom logic
//        if (prevSpec is WallSpec && !(newSpec is WallSpec))
//        {
//            //if wall removed, and bottom isn't wall, set to default tile
//            var bottomTile = chunk.GetTileSpecAtIndexSafe(relIdx.X, relIdx.Y-1);
//            if (bottomTile != null && !(bottomTile is WallSpec))
//            {
//                Point bottom = new Point(relIdx.X, relIdx.Y);
//                var botEl = chunk.tiles_.GetElement(bottom);
//                botEl.Spec = TileSpec.GetAt();
//                chunk.tiles_.SetElement(bottom, botEl);//TODO
//            }
//        }

        chunk.RefreshTiles();
    }

    public TileMapOfEntities(Entity entity, int w, int h) : base(entity)
    {
        var cells = new EntityTile[w,h];
        for (int y = 0; y <= cells.GetUpperBound(1); y++)
        for (int x = 0; x <= cells.GetUpperBound(0); x++)
        {
            cells[x, y] = new EntityTile(entity.Position+new Vector2(x,y));
        }
        
        tiles_ = new Grid<EntityTile>(cells);

        foreach (KeyValuePair<Point, EntityTile> pair in tiles_.LoopAll())
        {
            int x = pair.Key.X;
            int y = pair.Key.Y;
            pair.Value.SetNeighbors(
                tiles_.GetElement(new Point(x, y + 1)),
                tiles_.GetElement(new Point(x + 1, y)),
                tiles_.GetElement(new Point(x, y - 1)),
                tiles_.GetElement(new Point(x - 1, y))
                );
        }

    }

    public override void FinalizeComponent()
    {
        base.FinalizeComponent();
        throw null;
//        for (int y = 0; y <= tiles_.GetUpperBound(1); y++)
//        for (int x = 0; x <= tiles_.GetUpperBound(0); x++)
//        {
//            tiles_[x, y].Finalize();
//        }
    }

    public override void EntityChanged()
    {
        base.EntityChanged();
        throw null;
//        for (int y = 0; y <= tiles_.GetUpperBound(1); y++)
//        for (int x = 0; x <= tiles_.GetUpperBound(0); x++)
//        {
//            tiles_[x, y].SetPosition(entity.Position+new Vector2(x, y));
//        }
    }
    
    void RefreshTiles()
    {
        foreach (KeyValuePair<Point, EntityTile> pair in tiles_.LoopAll())
        {
            pair.Value.RefreshTile();
        }
    }

    public void SetAll(TileSpec ts)
    {
        foreach (KeyValuePair<Point, EntityTile> pair in tiles_.LoopAll())
        {
            pair.Value.Spec = ts;
        }
        RefreshTiles();
    }

    public void SetArea(Rectangle rect, TileSpec ts)
    {
        foreach (KeyValuePair<Point, EntityTile> pair in tiles_.LoopRect(rect))
        {
            pair.Value.Spec = ts;
        }
        RefreshTiles();
    }

    public void SetBounds(Rectangle rect, TileSpec ts)
    {
        foreach (KeyValuePair<Point, EntityTile> pair in tiles_.LoopRectBoundsClip(rect))
        {
            pair.Value.Spec = ts;
        }
        RefreshTiles();
    }

    //this is slow as heck, use only in editor, at runtime check if bottom != wall when removing
//    public static void UpdateWallsBottomsEditor(Vector2 pos)
//    {
//        var chunk = GetTileMapChunkForPosition(pos);
//        chunk?.UpdateWallsBottomsEditor();
//    }
//    void UpdateWallsBottomsEditor()
//    {
//        for (int y = 0; y <= tiles_.GetUpperBound(1); y++)
//        for (int x = 0; x <= tiles_.GetUpperBound(0); x++)
//        {
//            if (tiles_[x, y].Spec is WallSpec)
//            {
//                var bottomSpec = GetTileSpecAtIndexSafe(x, y-1);
//                if (bottomSpec != null && !(bottomSpec is WallSpec))
//                {
//                    tiles_[x, y-1].Spec = (tiles_[x, y].Spec as WallSpec).GetWallBottom(
//                        GetTileSpecAtIndexSafe(x-1, y),
//                        GetTileSpecAtIndexSafe(x+1, y)
//                    );
//                }
//            }
//        }
//        RefreshTiles();//FIXME TODO
//    }

    public TileMapOfEntities(Entity entity, byte[] serialData) : base(entity)
    {
        tiles_ = MessagePackSerializer.Deserialize<Grid<EntityTile>>(serialData);
    }
    
    public override ComponentData GetSerialData()
    {
        return new ComponentData(ComponentTypes.TileMapChunk, MessagePackSerializer.Serialize(tiles_));
    }
}