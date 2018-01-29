using System;
using System.Collections.Generic;
using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

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
public abstract class ManagedChunkedComponent<T> : Component
{
    protected static ushort CHUNK_SIZE = 10;

    protected static readonly Dictionary<Point, List<ManagedChunkedComponent<T>>> chunks_ = 
        new Dictionary<Point, List<ManagedChunkedComponent<T>>>();

    static ManagedChunkedComponent()
    {
        Dbg.onBeforeDebugDrawing += DrawDebug;
    }

    protected static List<ManagedChunkedComponent<T>> GetChunkEntityList(Point key)
    {
        if (chunks_.TryGetValue(key, out List<ManagedChunkedComponent<T>> ol))
            return ol;
        return null;
    }

    protected static List<ManagedChunkedComponent<T>> GetChunkEntityListForPos(Vector2 position)
    {
        return GetChunkEntityList(GetChunkKeyForPos(position));
    }

    protected static Point GetChunkKeyForPos(Vector2 position)
    {
        return new Point((position.X/CHUNK_SIZE).Settle(), (position.Y/CHUNK_SIZE).Settle());
    }
    public static List<Point> GetChunksInRect(RectF rect)
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

    public static List<ManagedChunkedComponent<T>> GetComponentsInRect(RectF rect)
    {
        List<Point> chunksToCheck = GetChunksInRect(rect);
        List<ManagedChunkedComponent<T>> retList = new List<ManagedChunkedComponent<T>>();
        foreach (Point chunkPos in chunksToCheck)
        {
            if (chunks_.ContainsKey(chunkPos) && chunks_[chunkPos] != null)
                foreach (ManagedChunkedComponent<T> component in chunks_[chunkPos])
                    if(rect.Contains(component.entity.Position))
                        retList.Add(component);
                        
        }
        return retList;
    }

    public static List<ManagedChunkedComponent<T>> GetAllComponents()
    {
        List<ManagedChunkedComponent<T>> retList = new List<ManagedChunkedComponent<T>>();
        foreach (KeyValuePair<Point, List<ManagedChunkedComponent<T>>> pair in chunks_)
        {
            retList.AddRange(pair.Value);
        }
        return retList;
    }


    protected ManagedChunkedComponent(Entity entity) : base(entity)
    {
        UpdateChunk();
    }
    
    private Point currentChunkPos_ = new Point(Int32.MaxValue, Int32.MaxValue);
    protected void UpdateChunk()
    {
        Point newChunkPos = GetChunkKeyForPos(entity.Position);

        // if we didn't change chunk, do nothing
        if (newChunkPos == currentChunkPos_)
            return;

        //Debug.WriteLine($"changing obj from {currentChunkPos_} to {newChunkPos}");

        TryRemoveFromCurrentChunk();

        // add to new chunk
        if (!chunks_.ContainsKey(newChunkPos))
        {
            //Debug.WriteLine($"creating new chunk for item at: {currentChunkPos_}");
            chunks_[newChunkPos] = new List<ManagedChunkedComponent<T>>(); // creates new if doesn't exist
        }

        chunks_[newChunkPos].Add(this); // adds obj to chunk at new position

        // store current chunk
        currentChunkPos_ = newChunkPos;
    }

    protected void TryRemoveFromCurrentChunk()
    {
        // cleaning up previous chunk stuff
        if (chunks_.ContainsKey(currentChunkPos_)) //if chunk at previous position exists
        {
            if (chunks_[currentChunkPos_] != null) //if list is ok
            {
                chunks_[currentChunkPos_].Remove(this); // remove from previous chunk

                if (chunks_[currentChunkPos_].Count == 0) //clean up if empty
                {
                    //Debug.WriteLine($"deleting chunk with no items: {currentChunkPos_}");
                    chunks_.Remove(currentChunkPos_);
                }
            }
        }
    }
    
    public override void EntityChanged()
    {
        base.EntityChanged();
        UpdateChunk();
    }

    public override void FinalizeComponent()
    {
        TryRemoveFromCurrentChunk();
        base.FinalizeComponent();
    }


    //debug
    protected static Color debugColor = Color.White;
    private static void DrawDebug()
    {
        foreach (KeyValuePair<Point, List<ManagedChunkedComponent<T>>> chunk in chunks_)
        {
            var pos = chunk.Key;
            var comp = chunk.Value;

            //draw region square
            var regionRect = new Rectangle(pos.X*CHUNK_SIZE, pos.Y*CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);
            Dbg.AddDebugRect(regionRect, debugColor, Randy.NextFloat(0.25f));

            Dbg.AddDebugText(comp.Count.ToString(), regionRect.Location.ToVector2(), debugColor);

            for (var index = 0; index < comp.Count; index++)
            {
                ManagedChunkedComponent<T> component = comp[index];
                //DebugHelper.AddDebugText(index.ToString(), component.entity.Position, debugColor);
            }
        }
    }
}





public class SoundPlayer : ManagedChunkedComponent<SoundPlayer>
{
    private const float SOUNDS_DIST = 16;

    private SoundEffectInstance effectInstance_;

    private bool lastInRange = false;
    private bool inRange = false;
    private void ResetCullState()
    {
        lastInRange = inRange;
        inRange = false;
    }
    private void SetInRange()
    {
        inRange = true;
    }
    private void ProcessChange()
    {
        if (lastInRange != inRange)
        {
            if (inRange)// became hearable / was unculled
            {
//                if(effectInstance_.IsLooped)
//                    effectInstance_.Play();
            }
            else //was culled out
            {
                effectInstance_.Stop();
            }
        }
        if (inRange)
        {
            float vol = GetVolumeForPosition(entity.Position);
            float pan = GetPanForPosition(entity.Position.X);
            effectInstance_.Volume = vol;
            effectInstance_.Pan = MathHelper.Clamp(pan,-1,1);
//            DebugHelper.AddDebugText($"{vol}, {pan},\n {effectInstance_.Volume}, {effectInstance_.Pan}", entity.Position, Color.White);
        }
    }

    private static float GetPanForPosition(float x)
    {
        return (x-Core.mainCam.Center.X)/SOUNDS_DIST/2;
    }

    protected static float GetVolumeForPosition(Vector2 pos)
    {
        return (SOUNDS_DIST - Vector2.Distance(Core.mainCam.Center, pos) + SOUNDS_DIST/2)/SOUNDS_DIST;
    }




    public static void PlayOneShotAt(SoundEffect sound, Vector2 position)
    {
        sound.Play(GetVolumeForPosition(position), 0, GetPanForPosition(position.X));
    }


    private string effectId_;

    public SoundPlayer(Entity entity, string effectId) : base(entity)
    {
        effectId_ = effectId;
        effectInstance_ = PipelineAssets.LoadAsset<SoundEffect>(effectId).CreateInstance();
    }

    public void Play()
    {
        if (inRange)
        {   
            effectInstance_.Stop(true);
            effectInstance_.Play();
        }
    }

    public static void CullSoundsInRect(RectF rect)
    {
        //TODO store in range list to check when stuff gets in/outta range
        //mark all as culled
        foreach (SoundPlayer player in GetAllComponents())
            player.ResetCullState();

        //unmark the ones in rect
        foreach (SoundPlayer player in GetComponentsInRect(rect))
            player.SetInRange();

        //process state changes
        foreach (SoundPlayer player in GetAllComponents())
            player.ProcessChange();

    }
    
    //TODO save more data
    public SoundPlayer(Entity entity, byte[] serialData) : this(entity, MessagePackSerializer.Deserialize<string>(serialData))
    {}

    public override ComponentData GetSerialData()
    {
        return new ComponentData(ComponentTypes.SoundPlayer, MessagePackSerializer.Serialize(effectId_));
    }
}