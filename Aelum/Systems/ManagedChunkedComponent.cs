using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

public abstract class ChunkedComponentSystem<T, TSystem>
   where T : ManagedChunkComponent<T, TSystem>
   where TSystem : ChunkedComponentSystem<T, TSystem>, new()
{
   protected static readonly ushort CHUNK_SIZE = 16; //NOTE: override in child systems constr
   public readonly Dictionary<Point, List<T>> chunks_ = new Dictionary<Point, List<T>>();
   
   protected static Color debugColor = Color.White;

   public void DrawDebug()
   {
      foreach (KeyValuePair<Point, List<T>> chunk in chunks_)
      {
         var pos = chunk.Key;
         var comp = chunk.Value;

         //draw region square
         var regionRect = new Rectangle(pos.X*CHUNK_SIZE, pos.Y*CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);
         Dbg.AddDebugRect(regionRect, debugColor, Randy.NextFloat(0.25f));

         Dbg.AddDebugText(comp.Count.ToString(), regionRect.Location.ToVector2(), debugColor);

         for (var index = 0; index < comp.Count; index++)
         {
            T component = comp[index];
            //DebugHelper.AddDebugText(index.ToString(), component.entity.Position, debugColor);
         }
      }
   }

   protected Point GetChunkKeyForPos(Vector2 position)
   {
      return new Point((position.X/CHUNK_SIZE).Settle(), (position.Y/CHUNK_SIZE).Settle());
   }

   protected List<T> GetChunkEntityList(Point key)
   {
      if (chunks_.TryGetValue(key, out List<T> ol))
         return ol;
      return null;
   }

   protected List<T> GetChunkEntityListForPos(Vector2 position)
   {
      return GetChunkEntityList(GetChunkKeyForPos(position));
   }

   public List<Point> GetChunksInRect(RectF rect)
   {
      // assemble list of unique chunks in rect
      List<Point> chunksInRect = new List<Point>();
        
      for (float rx = rect.X; rx < rect.Right + CHUNK_SIZE; rx += CHUNK_SIZE - 0.1f) //TODO FIXME
      for (float ry = rect.Y; ry < rect.Bottom + CHUNK_SIZE; ry += CHUNK_SIZE - 0.1f)
      {
         Point p = GetChunkKeyForPos(new Vector2(rx, ry));
         if (!chunksInRect.Contains(p))
            chunksInRect.Add(p);
      }
      return chunksInRect;
   }

   public List<T> GetComponentsInRect(RectF rect)
   {
      List<Point> chunksToCheck = GetChunksInRect(rect);
      List<T> retList = new List<T>();
      foreach (Point chunkPos in chunksToCheck)
         if (chunks_.ContainsKey(chunkPos) && chunks_[chunkPos] != null)//TODO use out value
            foreach (T component in chunks_[chunkPos])
               if(rect.Contains(component.entity.Position))
                  retList.Add(component);
      return retList;
   }

   public List<T> GetAllComponents()
   {
      List<T> retList = new List<T>();
      foreach (KeyValuePair<Point, List<T>> pair in chunks_)
      {
         retList.AddRange(pair.Value);
      }
      return retList;
   }

   internal void UpdateComponentChunk(T component)
   {
      Point newKey = GetChunkKeyForPos(component.entity.Position);

      // if we didn't change chunk, do nothing
      if (newKey == component.currentChunkPos_)
         return;

      //Debug.WriteLine($"changing obj from {currentChunkPos_} to {newChunkPos}");

      RemoveFromCurrentChunk(component);

      // add to new chunk
      if (!chunks_.ContainsKey(newKey))
      {
         //Debug.WriteLine($"creating new chunk for item at: {currentChunkPos_}");
         chunks_[newKey] = new List<T>(); // creates new if doesn't exist
      }

      chunks_[newKey].Add(component); // adds obj to chunk at new position

      // store current chunk
      component.currentChunkPos_ = newKey;
   }

   internal void RemoveFromCurrentChunk(T component)
   {
      Point currentKey = component.currentChunkPos_;
      // cleaning up previous chunk stuff
      if (chunks_.ContainsKey(currentKey)) //if chunk at previous position exists
      {
         if (chunks_[currentKey] != null) //if list is ok
         {
            chunks_[currentKey].Remove(component); // remove from previous chunk

            if (chunks_[currentKey].Count == 0) //clean up if empty
            {
               //Debug.WriteLine($"deleting chunk with no items: {currentChunkPos_}");
               chunks_.Remove(currentKey);
            }
         }
      }
   }
}

public abstract class ManagedChunkComponent<T, TSystem> : Component
   where T : ManagedChunkComponent<T, TSystem>
   where TSystem : ChunkedComponentSystem<T, TSystem>, new()
{
   public static TSystem DEFAULT_SYSTEM => SYSTEMS[0];
   public static readonly List<TSystem> SYSTEMS = new List<TSystem>{new TSystem()};
   
   static ManagedChunkComponent()
   {
      Dbg.onBeforeDebugDrawing += DEFAULT_SYSTEM.DrawDebug;
   }

   private byte systemIndex = 0;
   public TSystem System => SYSTEMS[systemIndex];
   internal Point currentChunkPos_ = new Point(Int32.MaxValue, Int32.MaxValue);

   protected ManagedChunkComponent(Entity entity, byte system = 0) : base(entity)
   {
      systemIndex = system;
      UpdateChunk();
   }

   protected void UpdateChunk()
   {
      SYSTEMS[systemIndex].UpdateComponentChunk((T)this);
   }
   
   public override void EntityChanged()
   {
      base.EntityChanged();
      UpdateChunk();
   }

   public override void FinalizeComponent()
   {
      SYSTEMS[systemIndex].RemoveFromCurrentChunk((T)this);
      base.FinalizeComponent();
   }
}













//TODO REMOVE THIS CLASS, rename above one
//public abstract class ManagedChunkedComponent<T> : Component
//{
//    protected static ushort CHUNK_SIZE = 10;
//
//    protected static readonly Dictionary<Point, List<ManagedChunkedComponent<T>>> chunks_ = 
//        new Dictionary<Point, List<ManagedChunkedComponent<T>>>();
//
//    static ManagedChunkedComponent()
//    {
//        Dbg.onBeforeDebugDrawing += DrawDebug;
//    }
//
//    protected static List<ManagedChunkedComponent<T>> GetChunkEntityList(Point key)
//    {
//        if (chunks_.TryGetValue(key, out List<ManagedChunkedComponent<T>> ol))
//            return ol;
//        return null;
//    }
//
//    protected static List<ManagedChunkedComponent<T>> GetChunkEntityListForPos(Vector2 position)
//    {
//        return GetChunkEntityList(GetChunkKeyForPos(position));
//    }
//
//    protected static Point GetChunkKeyForPos(Vector2 position)
//    {
//        return new Point((position.X/CHUNK_SIZE).Settle(), (position.Y/CHUNK_SIZE).Settle());
//    }
//    public static List<Point> GetChunksInRect(RectF rect)
//    {
//        // assemble list of unique chunks in rect
//        List<Point> chunksInRect = new List<Point>();
//        
//        for (float rx = rect.X; rx < rect.Right + CHUNK_SIZE; rx += CHUNK_SIZE - 0.1f) //TODO FIXME
//        for (float ry = rect.Y; ry < rect.Bottom + CHUNK_SIZE; ry += CHUNK_SIZE - 0.1f)
//        {
//            Point p = GetChunkKeyForPos(new Vector2(rx, ry));
//            if (!chunksInRect.Contains(p))
//                chunksInRect.Add(p);
//        }
//        return chunksInRect;
//    }
//
//    public static List<ManagedChunkedComponent<T>> GetComponentsInRect(RectF rect)
//    {
//        List<Point> chunksToCheck = GetChunksInRect(rect);
//        List<ManagedChunkedComponent<T>> retList = new List<ManagedChunkedComponent<T>>();
//        foreach (Point chunkPos in chunksToCheck)
//        {
//            if (chunks_.ContainsKey(chunkPos) && chunks_[chunkPos] != null)
//                foreach (ManagedChunkedComponent<T> component in chunks_[chunkPos])
//                    if(rect.Contains(component.entity.Position))
//                        retList.Add(component);
//                        
//        }
//        return retList;
//    }
//
//    public static List<ManagedChunkedComponent<T>> GetAllComponents()
//    {
//        List<ManagedChunkedComponent<T>> retList = new List<ManagedChunkedComponent<T>>();
//        foreach (KeyValuePair<Point, List<ManagedChunkedComponent<T>>> pair in chunks_)
//        {
//            retList.AddRange(pair.Value);
//        }
//        return retList;
//    }
//
//
//    protected ManagedChunkedComponent(Entity entity) : base(entity)
//    {
//        UpdateChunk();
//    }
//    
//    private Point currentChunkPos_ = new Point(Int32.MaxValue, Int32.MaxValue);
//    protected void UpdateChunk()
//    {
//        Point newChunkPos = GetChunkKeyForPos(entity.Position);
//
//        // if we didn't change chunk, do nothing
//        if (newChunkPos == currentChunkPos_)
//            return;
//
//        //Debug.WriteLine($"changing obj from {currentChunkPos_} to {newChunkPos}");
//
//        TryRemoveFromCurrentChunk();
//
//        // add to new chunk
//        if (!chunks_.ContainsKey(newChunkPos))
//        {
//            //Debug.WriteLine($"creating new chunk for item at: {currentChunkPos_}");
//            chunks_[newChunkPos] = new List<ManagedChunkedComponent<T>>(); // creates new if doesn't exist
//        }
//
//        chunks_[newChunkPos].Add(this); // adds obj to chunk at new position
//
//        // store current chunk
//        currentChunkPos_ = newChunkPos;
//    }
//
//    protected void TryRemoveFromCurrentChunk()
//    {
//        // cleaning up previous chunk stuff
//        if (chunks_.ContainsKey(currentChunkPos_)) //if chunk at previous position exists
//        {
//            if (chunks_[currentChunkPos_] != null) //if list is ok
//            {
//                chunks_[currentChunkPos_].Remove(this); // remove from previous chunk
//
//                if (chunks_[currentChunkPos_].Count == 0) //clean up if empty
//                {
//                    //Debug.WriteLine($"deleting chunk with no items: {currentChunkPos_}");
//                    chunks_.Remove(currentChunkPos_);
//                }
//            }
//        }
//    }
//    
//    public override void EntityChanged()
//    {
//        base.EntityChanged();
//        UpdateChunk();
//    }
//
//    public override void FinalizeComponent()
//    {
//        TryRemoveFromCurrentChunk();
//        base.FinalizeComponent();
//    }
//
//
//    //debug
//    protected static Color debugColor = Color.White;
//    private static void DrawDebug()
//    {
//        foreach (KeyValuePair<Point, List<ManagedChunkedComponent<T>>> chunk in chunks_)
//        {
//            var pos = chunk.Key;
//            var comp = chunk.Value;
//
//            //draw region square
//            var regionRect = new Rectangle(pos.X*CHUNK_SIZE, pos.Y*CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);
//            Dbg.AddDebugRect(regionRect, debugColor, Randy.NextFloat(0.25f));
//
//            Dbg.AddDebugText(comp.Count.ToString(), regionRect.Location.ToVector2(), debugColor);
//
//            for (var index = 0; index < comp.Count; index++)
//            {
//                ManagedChunkedComponent<T> component = comp[index];
//                //DebugHelper.AddDebugText(index.ToString(), component.entity.Position, debugColor);
//            }
//        }
//    }
//}