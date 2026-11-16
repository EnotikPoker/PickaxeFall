using System;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public static class InputLightingReleaseChecks
    {
        public static void Validate(GameBalance balance)
        {
            int checks=0;
            void Check(bool ok,string label){checks++;if(!ok)throw new Exception("Input/light data: "+label);}
            var settings=new GameSettings(true,false);
            Check(settings.MineBrightness==1,"default brightness");
            settings.SetMineBrightness(1.2f);Check(Mathf.Abs(settings.MineBrightness-1.2f)<.001f,"brightness applies");
            settings.SetMineBrightness(float.NaN);Check(settings.MineBrightness==1,"NaN recovers");
            settings.SetMineBrightness(-10);Check(settings.MineBrightness==1,"lower bound");
            settings.SetMineBrightness(20);Check(settings.MineBrightness==1.3f,"upper bound");
            settings.Reset();Check(settings.MineBrightness==1,"reset restores brightness");
            foreach(MapId map in Enum.GetValues(typeof(MapId)))for(int depth=0;depth<=500;depth+=5)foreach(bool solid in new[]{false,true})foreach(float gain in new[]{1f,1.3f,float.NaN})
            {
                var value=MineLighting.Readable(MineLighting.Ambient(map,depth/45f,solid),gain);
                Check(value.r>=191&&value.g>=191&&value.b>=191,"ambient floor");
                Check(value.a==255,"opaque multiplicative light field");
            }
            Check(balance.minimumReboundSpeed==3&&balance.maximumReboundSpeed==6.4f,"small dry rebound increase");
            Debug.Log("PICKAXEFALL_INPUT_LIGHTING_DATA_OK: "+checks);
        }
    }
}
