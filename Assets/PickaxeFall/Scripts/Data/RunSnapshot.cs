using System;
using UnityEngine;

namespace PickaxeFall
{
    // Replaced as a whole at save time; never mutate an already published snapshot.
    [Serializable] public sealed class RunSnapshot
    {
        public int version=5,seed,ticks,depth,blocks,benches,toolCount,selectedTool,selectedTier;
        public MapId map;
        public SpawnerState[] spawners;
        public string balanceJson;
        public BenchVisit[] benchVisits;
        public LiquidCellSnapshot[] liquids;
        public bool rescueUsed,dashUnlocked,plungeUnlocked;
        public int[] levels,rollStates;
        public long[] loot,attempts,successes;
        public float energy,energyEarned;
        public RunBenefits benefits;
        public ToolSnapshot[] tools;
        public CellSnapshot[] cells;
        public void Validate()
        {
            if(version<=4 && version>=1 && levels?.Length==25 && rollStates?.Length==17 && attempts?.Length==17 && successes?.Length==17)
            {
                Array.Resize(ref levels,Progression.KeyCount);var rolls=new EffectRolls();rolls.Reset(seed);var extended=rolls.Capture();Array.Copy(rollStates,extended,17);rollStates=extended;
                Array.Resize(ref attempts,Progression.KindCount);Array.Resize(ref successes,Progression.KindCount);
            }
            if((version<1 || version>5) || ticks<0 || depth<0 || toolCount<1 || toolCount>5 || selectedTool<0 || selectedTool>=5 || selectedTier<0 || selectedTier>4 ||
                string.IsNullOrEmpty(balanceJson) || levels==null || levels.Length!=Progression.KeyCount || loot==null || loot.Length!=ResourceInventory.SlotCount ||
                rollStates==null || rollStates.Length!=Progression.KindCount || attempts==null || attempts.Length!=Progression.KindCount || successes==null || successes.Length!=Progression.KindCount ||
                tools==null || tools.Length!=5 || cells==null || benefits==null || !Finite(energy) || !Finite(energyEarned) || energy<0 || energyEarned<0)
                throw new InvalidOperationException("Повреждены данные незавершённого спуска.");
            if(!Enum.IsDefined(typeof(MapId),map))throw new InvalidOperationException("Invalid saved world.");
            if(spawners!=null)foreach(var s in spawners)if(s==null || s.row<0 || s.hits<1 || s.hits>4 || s.lastHits?.Length!=5 || !Finite(s.until) || s.until<0)throw new InvalidOperationException("Invalid trial state.");
            var profile=new PlayerProfile {upgrades=levels};profile.Validate();
            bool alive=false;
            for(int i=0;i<tools.Length;i++)
            {
                var t=tools[i];
                if(t==null || t.index!=i || t.tier<0 || t.tier>4 || t.originalTier<0 || t.originalTier>4 || t.durability<0 || !Finite(t.position.x) || !Finite(t.position.y) || !Finite(t.velocity.x) || !Finite(t.velocity.y) || !Finite(t.rotation) || !Finite(t.angularVelocity) || !Finite(t.lavaWear) || t.lavaWear<0 || t.lavaWear>=1)throw new InvalidOperationException("Повреждена кирка сохранённого спуска.");
                alive|=t.alive && t.durability>0;
            }
            if(!alive || !tools[selectedTool].alive)throw new InvalidOperationException("В сохранённом спуске нет выбранной живой кирки.");
            foreach(var cell in cells)if(cell==null || cell.row< -2 || !Enum.IsDefined(typeof(BlockKind),cell.kind) || cell.health<0 || !Finite(cell.litUntil))throw new InvalidOperationException("Повреждена шахта сохранённого спуска.");
            for(int i=0;i<Progression.KindCount;i++)if(rollStates[i]==0 || attempts[i]<0 || successes[i]<0 || successes[i]>attempts[i])throw new InvalidOperationException("Повреждена случайность сохранённого спуска.");
            if(liquids!=null)foreach(var cell in liquids)if(cell==null || cell.row< -2 || cell.amount<0 || cell.amount>8 || cell.kind!=BlockKind.Water && cell.kind!=BlockKind.Lava)throw new InvalidOperationException("Повреждена жидкость сохранённого спуска.");
            foreach(long count in loot)if(count<0)throw new InvalidOperationException("Повреждена добыча сохранённого спуска.");
        }
        static bool Finite(float x)=>!float.IsNaN(x) && !float.IsInfinity(x);
    }
    [Serializable] public sealed class CellSnapshot
    {
        public int x,row,health;
        public BlockKind kind;
        public bool broken;
        public float litUntil;
    }
    [Serializable] public sealed class ToolSnapshot
    {
        public int tier,originalTier,index,durability,strikes,naturalStrikes,hops,step,remaining,ability;
        public bool alive;
        public Vector2 position,velocity,incomingVelocity;
        public float rotation,angularVelocity,incomingAngular,lastRebound,abilityUntil,momentumUntil,nextTrail,lavaWear;
        public ContactSnapshot[] contacts;
    }
    [Serializable] public sealed class ContactSnapshot
    {
        public Vector2Int cell;
        public bool armed;
        public int hitStep,seenStep;
        public Vector2 normal,localPoint;
    }
}
