using System;
using System.Text;
using Crestron.SimplSharp;                         				// For Basic SIMPL# Classes
using Newtonsoft.Json;                                          //Thanks to Neil Colvin. Full Library @ http://www.nivloc.com/downloads/crestron/SSharp/
using Crestron.SimplSharp.CrestronIO;
using System.Collections.Generic;
//using SpmLibrary;

namespace JsonDb
{
    public class LightsJsonDb
    {
        public string RmName;
        public ushort RmID;                  //Control ID or other ID     
        public int Count;                   //Total number of sources found. I'm passing this back to SIMPL+ to make the loop dynamic
        //private string DaString;
        public SceneConfig MySceneConfig;
        private string JsonString;
        //private JsonDatabase MyJsonDatabase;

        // Public delegates
        public OutputCirValDel OutputCirVal { get; set; }
        //public OutputCirCountDel OutputCirCount { get; set; }
        public delegate void OutputCirValDel(ushort value, ushort circuitIndex, ushort roomIndex);
        //public delegate void OutputCirCountDel(int roomIndex, int sceneIndex);

        /*Pass the FilePath from SIMPL+ then read in the file.
        Create the JSON Object and use the library to deserialize it into 
        our classes.
        */

        public void Reader()
        {
            //MyJsonDatabase = new JsonDatabase<SceneConfig>();
            if (File.Exists(FilePath))       //Ok make sure the file is there
            {
                StreamReader JsonFile = new StreamReader(FilePath);
                JsonString = JsonFile.ReadToEnd();
                JsonFile.Close();
                CrestronConsole.PrintLine("File found" + JsonString + "\n\r");    //Generate error
            }
            else
            {
                CrestronConsole.PrintLine("File Not found\n\r");    //Generate error
                JsonString = "";

            }

            CrestronConsole.PrintLine("Extractor Starting...\n\r");    //Generate error
            MySceneConfig = JsonConvert.DeserializeObject<SceneConfig>(JsonString);
            CrestronConsole.PrintLine("Extractor Finished...\n\r");    //Generate error 
        }

        public string FilePath
        {
            get;
            set;
        }

        public ushort getCircuitValue(ushort roomIndex, ushort sceneIndex, ushort cirIndex)
        {
            CrestronConsole.PrintLine("Get Circuit Value rm={0} scene={1} cir={2} value={3}\n\r", roomIndex, sceneIndex, cirIndex, MySceneConfig.Rooms[roomIndex].Scenes[sceneIndex].Circuits[cirIndex].Value);
            return MySceneConfig.Rooms[roomIndex].Scenes[sceneIndex].Circuits[cirIndex].Value;
        }

        public void setCircuitValue(ushort roomIndex, ushort sceneIndex, ushort cirIndex, ushort value)
        {
            CrestronConsole.PrintLine("Set Circuit Value rm={0} scene={1} cir={2} value={3}\n\r", roomIndex, sceneIndex, cirIndex, value);
            MySceneConfig.Rooms[roomIndex].Scenes[sceneIndex].Circuits[cirIndex].Value = value;              
        }

        public void storeObjToFile()
        {
            StreamWriter file = new StreamWriter(FilePath);
            string ConfigSave = JsonConvert.SerializeObject(MySceneConfig);
            file.Write(ConfigSave);
            file.Flush();
            file.Close();
        }

        public class SceneConfig
        {
            [JsonProperty("Rooms")]
            public IList<Room> Rooms { get; set; }
        }

        public class Room
        {
            [JsonProperty("RoomName")]
            public string RoomName { get; set; }
            [JsonProperty("RoomID")]
            public ushort RoomID { get; set; }
            [JsonProperty("Scenes")]
            public IList<Scene> Scenes { get; set; }
        }

        public class Scene
        {
            [JsonProperty("SceneName")]
            public string SceneName { get; set; }
            [JsonProperty("SceneID")]
            public ushort SceneID { get; set; }
            [JsonProperty("Circuits")]
            public IList<Circuit> Circuits { get; set; }
        }

        public class Circuit
        {
            [JsonProperty("CircuitName")]
            public string CircuitName { get; set; }
            [JsonProperty("CircuitValue")]
            public ushort Value { get; set; }
        }
    }
}