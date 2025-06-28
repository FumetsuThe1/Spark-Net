using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SparkNet.Classes
{
    public class Emotion
    {
        int suppressionLevel = 0;

        int joyLevel = 0;
        int angerLevel = 0;
        int sadnessLevel = 0;
        int fearLevel = 0;
        int disgustLevel = 0;
        int surpriseLevel = 0;


        bool moduleLoaded = true;


        public string FormulateSentence(string String, bool disableEmotion = false)
        {
            if (moduleLoaded && !disableEmotion)
            {
                string[] words = String.Split(' ');
                foreach (string word in words)
                {
                    Classes.Spark.DebugLog("Word: " + word);
                }
                return String;
            }
            else
            {
                return String;
            }
        }
    }
}
