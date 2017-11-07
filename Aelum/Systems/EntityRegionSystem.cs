#if ORIGIN_SHIFT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MessagePack;
using Microsoft.Xna.Framework;

public partial class Entity
{
    public static void KeepOriginForPosition(Vector2 position)
    {
        EntityChunkRegionSystem.KeepOriginForPosition(position); //TODO cache and run outside behavior update loop
    }

    public static void DebugDrawEntityChunks()
    {
        EntityChunkRegionSystem.DebugDrawEntityChunks();
    }

    public static Action<Vector2> OnOriginShift;

    private static class EntityChunkRegionSystem
    {
        class Chunk
        {
            public Chunk(int chunkIndexInRegion, int absRegionX)
            {
                this.chunkIndexInRegion = chunkIndexInRegion;
                this.absRegionX = absRegionX;
                isLoaded = false;
            }

            public Vector2 GetWorldCenter(int relRegionX)
            {
                return new Vector2(
                    RelativeRegionAndChunkIndexToWorld(relRegionX, chunkIndexInRegion) + CHUNK_SIZE/2f
                    ,0
                    );
            }

            private readonly int chunkIndexInRegion;

            private string Id => $"{absRegionX}_{chunkIndexInRegion}";

            private bool isLoaded;
            public int absRegionX { get; private set; }

            public List<Entity> entities { get; } = new List<Entity>();

            public void UnloadChunk(int relRegionX)
            {
                List<entityData> listToStoreEntityDatas = new List<entityData>();

                while (entities.Count>0)
                {
                    Entity e = entities[entities.Count-1];
                    if (e.persistent)
                        listToStoreEntityDatas.Add(e.GetCereal(
                            new Vector2(RelativeRegionAndChunkIndexToWorld(relRegionX, chunkIndexInRegion), 0)));
                    if (e.shifts)
                        e.Dispose();
                    else
                        entities.Remove(e); //if entity doesn't shift, parent is responsible for disposing
                }
                //entities.Clear();

                //saving data
                if (listToStoreEntityDatas.Count > 0)
                {
                    byte[] chunkSerialData = MessagePackSerializer.Serialize(listToStoreEntityDatas);
                    savedChunks[Id] = chunkSerialData;


    //                File.WriteAllBytes(DATADIR + Id, chunkSerialData);
    //                
    ////                #if DEBUG
    //                string dbgText = $"DEBUG INFORMATION. Entity count: {listToStoreEntityDatas.Count}, specifics:";
    //                //File.WriteAllText(DATADIR + Id + "_debug", MessagePackSerializer.ToJson(chunkSerialData));
    //                foreach (entityData entityData in listToStoreEntityDatas)
    //                {
    //                    dbgText = string.Concat(dbgText, $"\npos: {entityData.position.ToVector2()}, rot: {entityData.rotation}");
    //                    foreach (ComponentData data in entityData.components)
    //                    {
    //                        dbgText = string.Concat(dbgText, $"\n   + component: {Enum.GetName(typeof(ComponentTypes), data.typeId)}, data: {MessagePackSerializer.ToJson(data.serialData)}");
    //                    }
    //                }
    //                File.WriteAllText(DATADIR + Id + "_debug", dbgText);
    //                #endif

                }
            }

            public void MarkAsNew()
            {
                isLoaded = false;
            }

            public void SetRegionX(int absRegionX)
            {
                this.absRegionX = absRegionX;
            }

            public bool TryLoadChunk(int relRegionX)
            {
                if (isLoaded) return false;
            

                List<entityData> existingData = null;


                if (savedChunks.ContainsKey(Id))
                {
                    existingData = MessagePackSerializer.Deserialize<List<entityData>>(savedChunks[Id]);
                    DebugHelper.AddDebugText("LOADED",
                        new Vector2(RelativeRegionAndChunkIndexToWorld(relRegionX,chunkIndexInRegion),-1),
                        Color.White, 30);
                }


    //            if (File.Exists(DATADIR + Id))
    //            {
    //                Debug.WriteLine("data file exists, loading...");
    //                existingData = MessagePackSerializer.Deserialize<List<entityData>>(File.ReadAllBytes(DATADIR+Id));
    //            }
            
                if (existingData != null)
                {
                    foreach (entityData data in existingData)
                        new Entity(data, new Vector2(RelativeRegionAndChunkIndexToWorld(relRegionX, chunkIndexInRegion), 0));
                }
                else
                {
                    Debug.WriteLine($"there's no existing data for chunk {Id}, calling generation");
                    for (int i = 0; i < CHUNK_SIZE; i++)
                    {
    //                    float worldChunkStartPos = RelativeRegionAndChunkIndexToWorld(relRegionX,chunkIndexInRegion);
    //                    
    //                    DebugHelper.AddDebugText("GEN",
    //                        new Vector2(worldChunkStartPos,-1),
    //                        Color.Yellow, 30);
    //
    //                    var e = new Entity(new Vector2(worldChunkStartPos + i, 4+i));
    //                    var b = new DynamicBody(e, true);
    //
    //                    new Rotator(e, (r.NextDouble() > 0.9) ? 1 : (r.NextDouble() > 0.8) ? -1 : 0);
    //
    //                    b.CreateCollider(new rectangleColliderData(Vector2.One.ToVec2F()));
    //
    //                    //b.CreateCollider(new circleColliderData(0.3f, 1, Vector2.One.ToVec2F()));
    //
    //                    var ce = new Entity(new Vector2(worldChunkStartPos + i, 4+i+1.5f));
    //                    var cb = new StaticBody(ce);
    ////                    cb.CreateCollider(new circleColliderData(0.3f));
    //                    cb.CreateCollider(new rectangleColliderData(Vector2.One.ToVec2F()));
    //                    new ICANHAZNAME(ce, $"{worldChunkStartPos}_{i}");
    //
    //
    //                    var cont = new EntityContainer(e);
    //                    cont.AddChild(ce);


    //                    var ce2 = new Entity(new Vector2(worldChunkStartPos + i, 4+i+3f));
    //                    var cb2 = new StaticBody(ce2);
    //                    cb2.CreateCollider(new rectangleColliderData(Vector2.One.ToVec2F()));
    //

    //                    var cont2 = new EntityContainer(ce);
    //                    cont2.AddChild(ce2);

    //                    e.Rotation -= r.NextDouble().ToFloat()/3f;

                    }

                }
            
                isLoaded = true;
                return true;
            }
        
            static Random r = new Random(42);

            public override string ToString(){return Id;}
        }

        private const int CHUNK_LOG_SIZE = 6;//3;//TODO
        private const int CHUNK_SIZE = 1 << CHUNK_LOG_SIZE;

        private const int REGION_LOG_SIZE_IN_CHUNKS = 1;
        private const int REGION_SIZE_IN_CHUNKS = 1 << REGION_LOG_SIZE_IN_CHUNKS;
        private const int REGION_SIZE_IN_METERS = REGION_SIZE_IN_CHUNKS * CHUNK_SIZE;

        private static int currentOriginInRegions = 0;

        private static Chunk[] chunks_ = new Chunk[REGION_SIZE_IN_CHUNKS*4];


        //TODO remove this dbg shit
        private static Dictionary<string, byte[]> savedChunks = new Dictionary<string, byte[]>();

        private const string DATADIR = "mapdata\\";
        
        static EntityChunkRegionSystem()
        {
            Directory.CreateDirectory(DATADIR);
            InitEntityChunks();
        }

        public static void KeepOriginForPosition(Vector2 position)
        {
            Point pos = position.Settle();

            //shift when appropriate
            Point shiftDirection = Point.Zero;
            if (pos.X > REGION_SIZE_IN_METERS) shiftDirection = new Point(pos.X/REGION_SIZE_IN_METERS,0);
            else if (pos.X < -REGION_SIZE_IN_METERS) shiftDirection = new Point(pos.X/REGION_SIZE_IN_METERS,0);

            // up to 100,000 from origin ok on nvidia 500 series... let's just shift every 10,000k then //todo

            if (shiftDirection != Point.Zero) //FIXME origin shift atm is slow and naive
            {
                // get previous regions range
                int prevAbsRegionsStart = currentOriginInRegions;
                int prevAbsRegionsEnd = currentOriginInRegions + 4;


                // store regions shift amount
                currentOriginInRegions += shiftDirection.X;


                // get current regions range
                int curAbsRegionsStart = currentOriginInRegions;
                int curAbsRegionsEnd = currentOriginInRegions + 4;


                // cleanup regions outta range
                for (var i = 0; i < chunks_.Length; i++)
                {
                    Chunk chunk = chunks_[i];
                    int relRegionX = i >> REGION_LOG_SIZE_IN_CHUNKS;

                    //if chunk abs region is not in current bounds
                    if (chunk.absRegionX < curAbsRegionsStart || chunk.absRegionX >= curAbsRegionsEnd)
                    {
                        chunk.UnloadChunk(relRegionX);
                    }
                }

                // translate all entities
                Vector2 shift = shiftDirection.ToVector2() * -REGION_SIZE_IN_METERS;
                Entity.ShiftAllEntities(shift);
                OnOriginShift?.Invoke(shift);

                // mark new chunks for loading
                for (var i = 0; i < chunks_.Length; i++)
                {
                    int relRegionX = i >> REGION_LOG_SIZE_IN_CHUNKS;
                    int absRegionX = currentOriginInRegions + relRegionX;

                    Chunk chunk = chunks_[i];
                    chunk.SetRegionX(absRegionX);

                    //load new chunks
                    if (chunk.absRegionX < prevAbsRegionsStart || chunk.absRegionX >= prevAbsRegionsEnd)
                    {
                        chunk.MarkAsNew();
                    }

                    int relChunkX = i % REGION_SIZE_IN_CHUNKS;
                    DebugHelper.AddDebugText(absRegionX.ToString(), chunk.GetWorldCenter(relRegionX) - Vector2.UnitY * 8,Color.White);
                    DebugHelper.AddDebugText(relChunkX.ToString(), chunk.GetWorldCenter(relRegionX) - Vector2.UnitY * 12,Color.Lime);
                    DebugHelper.AddDebugText(chunk.absRegionX.ToString(), chunk.GetWorldCenter(relRegionX) - Vector2.UnitY * 18,Color.Yellow);
                }
            }

            // load as appropriate TODO culling
            for (var i = 0; i < chunks_.Length; i++)
            {
                chunks_[i].TryLoadChunk(i >> REGION_LOG_SIZE_IN_CHUNKS);
            }

        }
    

        private static void InitEntityChunks()
        {
            //TODO load at correct starting position
            currentOriginInRegions = 0;

            for (var i = 0; i < chunks_.Length; i++)
            {
                chunks_[i] = new Chunk(i % REGION_SIZE_IN_CHUNKS, currentOriginInRegions + i / REGION_SIZE_IN_CHUNKS);
            }
        }

        public static Point GetChunkForPosition(Vector2 position)
        {
            return new Point(
                (position.X.Settle() >> CHUNK_LOG_SIZE) + REGION_SIZE_IN_CHUNKS * 2, // same as:  pos.Y / CHUNK_SIZE
                0
            );
        }
    
        private static int RelativeRegionAndChunkIndexToWorld(int relRegion, int chunkIdx)
        {
            int relativeRegion = -2 * REGION_SIZE_IN_METERS + relRegion * REGION_SIZE_IN_METERS;
            return relativeRegion + chunkIdx * CHUNK_SIZE;
        }

        public static void UpdateChunkSystemForEntity(Entity e) //maintains chunks
        {
            int lastChunkPos = GetChunkForPosition(e.LastPosition).X;
            int curChunkPos = GetChunkForPosition(e.Position).X;
        
            //if entity didn't change chunk
            if (lastChunkPos == curChunkPos) return;

            if (lastChunkPos >= 0 && lastChunkPos < chunks_.Length) //if last chunk pos is valid
            {
                var lastChunk = chunks_[lastChunkPos];
                lastChunk.entities.Remove(e);
            }

            if (curChunkPos >= 0 && curChunkPos < chunks_.Length) //if chunk pos is valid
            {
                var curChunk = chunks_[curChunkPos];
                if(!curChunk.entities.Contains(e))
                    curChunk.entities.Add(e);
            }
        }

        public static bool IsChunkIndexInBounds(int index)
        {
            if (index >= 0 && index < REGION_SIZE_IN_CHUNKS * 4)
                return true;
            return false;
        }

        public static void RemoveEntityFromSystem(Entity e)
        {
            int chunkPos = GetChunkForPosition(e.Position).X;
            if (IsChunkIndexInBounds(chunkPos))
            {
                chunks_[chunkPos].entities.Remove(e);
            }
        }

        public static void DebugDrawEntityChunks()
        {
            for (var i = 0; i < chunks_.Length; i++)
            {
                Chunk chunk = chunks_[i];
            
                Vector2 chunkStart = new Vector2(REGION_SIZE_IN_METERS*-2 + i* CHUNK_SIZE, 0);

                int curRegion = (i >> REGION_LOG_SIZE_IN_CHUNKS) +currentOriginInRegions;

                // color according to region
                Color c = Color.Red;
                if(curRegion == 1)c=Color.Lime;
                else if (curRegion == 2)c=Color.Yellow;
                else if (curRegion == 3)c=Color.Cyan;

                DebugHelper.AddDebugText(
                    $"regn:{curRegion}\narri:{i}\ncnki:{i% REGION_SIZE_IN_CHUNKS}\nents:{chunk.entities.Count}"
                    , chunkStart+Vector2.UnitY*-6, c);

                DebugHelper.AddDebugLine(chunkStart, chunkStart+Vector2.UnitY* CHUNK_SIZE, new Color(0,1,0,0.25f));

                foreach (Entity entity in chunk.entities)
                {
                    //DebugHelper.AddDebugLine(chunkStart, entity.Position, Color.Yellow);
                    //DebugHelper.AddDebugText(GetChunkForPosition(entity.Position).X.ToString(), entity.Position, c);
                }
            }
        }
    }
}
#endif