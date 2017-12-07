//Please uncomment the #define line below if you want to include the sample code 
// in the compiled output.
// for the sample to work, you'll have to add a reference to the SimplSharpPro.UI dll to your project.
//#define IncludeSampleCode

using System;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       				// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    		// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.UI;                    			// For UI Devices. Please include the 
using Crestron.SimplSharpPro.Keypads;
using Crestron.SimplSharpPro.Lighting;
using Crestron.SimplSharpPro.Lighting.Din;
using JsonDb;

namespace GrahamRoad
{
    public class ControlSystem : CrestronControlSystem
    {
        // Define local variables ...

        // devices
        public CrestronApp myCrestronApp;
        public C2nCbfP kitchenKpByDoor;
        public C2nCbfP kitchenKpByDining;
        public Din1DimU4 kitchenDimu4;

        // class variables
        private static uint rampRaiseLower = 1000;      // 10s
        private static uint rampScene = 200;            // 2s
        string lightsDbFilePath = "\\USER\\scenes.txt";
        LightsJsonDb lightsDb = new LightsJsonDb();

        /// <summary>
        /// Constructor of the Control System Class. Make sure the constructor always exists.
        /// If it doesn't exit, the code will not run on your 3-Series processor.
        /// </summary>
        public ControlSystem()
            : base()
        {
            try
            {
                // Set the number of threads which you want to use in your program - At this point the threads cannot be created but we should
                // define the max number of threads which we will use in the system.
                // the right number depends on your project; do not make this number unnecessarily large
                Thread.MaxNumberOfUserThreads = 20;                                              
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }          
        }

        /// <summary>
        /// Overridden function... Invoked before any traffic starts flowing back and forth between the devices and the 
        /// user program. 
        /// This is used to start all the user threads and create all events / mutexes etc.
        /// This function should exit ... If this function does not exit then the program will not start
        /// </summary>
        public override void InitializeSystem()
        {
            // cresnet devices       
            kitchenKpByDoor = new C2nCbfP(0x25, this);
            kitchenKpByDoor.ButtonStateChange += new ButtonEventHandler(myKp_ButtonChange);
            kitchenKpByDoor.Register();

            kitchenKpByDining = new C2nCbfP(0x26, this);
            kitchenKpByDining.ButtonStateChange += new ButtonEventHandler(myKp_ButtonChange);
            kitchenKpByDining.Register();

            kitchenDimu4 = new Din1DimU4(0x86, this);
            //kitchenDimu4.BaseEvent += new BaseEventHandler(kitchenDimu4_LoadStateChange);
            kitchenDimu4.Register();

            // ethernet devices
            myCrestronApp = new CrestronApp(0x04, this);
            myCrestronApp.SigChange += new SigEventHandler(myCrestronApp_SigChange);
            myCrestronApp.ParameterProjectName.Value = "Graham Road iPhone";
            myCrestronApp.Register();

            try
            {
                lightsDb.FilePath = lightsDbFilePath;
                lightsDb.Reader();
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("Initialise System: Failed to load lights db: {0}\r\n", ex);   
            }
            
        }

        void kitchenDimu4_LoadStateChange(GenericDevice lightingObject, BaseEventArgs args)
        {
            // use this structure to react to the different events
            /*switch (args.EventId)
            {
                case LoadEventIds.LevelInputChangedEventId:
                    myCrestronApp.UShortInput[args.Load].UShortValue = kitchenDimu4.DinLoads[args.Load].LevelOut.UShortValue;
                    break;

                default:
                    break;
            }*/
        }

        void digSigFb(BasicTriList currentDevice, uint[] digArray, bool state)
        {
            for(uint i=0; i<=digArray.Length; i++)
            {
                currentDevice.BooleanInput[digArray[i]].BoolValue = state;              
            }
        }

        void kpBtnFb(C2nCbfP currentDevice, uint[,] btnArray)
        {
            for (uint i = 0; i <= btnArray.Length; i++)
            {
                switch (btnArray[i, 1])
                {
                    case 1: currentDevice.Feedbacks[btnArray[i, 0]].State = true;
                        break;
                    case 0: currentDevice.Feedbacks[btnArray[i, 0]].State = false;
                        break;
                }
             
                CrestronConsole.PrintLine("Button{0}'s state is {1} BtnArray={2}\r\n", btnArray[i, 0], btnArray[i, 1], i);
            }
        }

        void myCrestronApp_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            // determine what type of sig has changed
            switch (args.Sig.Type)
            {
                // a bool (digital) has changed
                case eSigType.Bool:
                    // determine if the bool sig is true (digital high, press) or false (digital low, release)
                    if (args.Sig.BoolValue)		// press
                    {
                        // determine what sig (join) number has chagned
                        switch (args.Sig.Number)
                        {
                            // scene recall
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                            {
                                for (ushort i = 1; i <= 4; i++)
                                {
                                    CrestronConsole.PrintLine("Dimu4 Cir{0} Level = {1}\r\n", i, lightsDb.getCircuitValue(Convert.ToUInt16(0), Convert.ToUInt16(args.Sig.Number - 1), Convert.ToUInt16(i - 1)));
                                    kitchenDimu4.DinLoads[i].LevelIn.CreateRamp(lightsDb.getCircuitValue(Convert.ToUInt16(0), Convert.ToUInt16(args.Sig.Number - 1), Convert.ToUInt16(i - 1)), rampScene);
                                }

                                //fb (interlock)
                                digSigFb(currentDevice, new uint[] { 1, 2, 3, 4 }, false);
                                digSigFb(currentDevice, new uint[] { args.Sig.Number }, true);
                                break;
                            }
                            // scene store
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            {
                                for (ushort i = 1; i <= 4; i++)
                                {
                                    lightsDb.setCircuitValue(1, Convert.ToUInt16(args.Sig.Number), i, kitchenDimu4.DinLoads[i].LevelOut.UShortValue);
                                    //digSigFb(currentDevice, new uint[] { args.Sig.Number }, true);                                   
                                }
                                break;
                            }
                            case 11: // circuit1 Raise Start                    
                            case 12: // circuit2 Raise Start
                            case 13: // circuit3 Raise Start
                            case 14: // circuit4 Raise Start
                            {
                                kitchenDimu4.DinLoads[args.Sig.Number - 10].LevelIn.CreateRamp(65535, rampRaiseLower);
                                break;
                            }
                            case 15: // circuit1 Lower Start
                            case 16: // circuit2 Lower Start
                            case 17: // circuit3 Lower Start
                            case 18: // circuit4 Lower Start
                            {
                                kitchenDimu4.DinLoads[args.Sig.Number - 14].LevelIn.CreateRamp(0, rampRaiseLower);
                                break;
                            }                                
                            default:
                                CrestronConsole.PrintLine("Button{0}'s Not Programmed!!\r\n", args.Sig.Number);   
                                break;
                        }
                    }
                    else						// release
                    {
                        // determine what sig (join) number has changed
                        switch (args.Sig.Number)
                        {
                            case 11: // circuit1 Raise Stop                    
                            case 12: // circuit2 Raise Stop
                            case 13: // circuit3 Raise Stop
                            case 14: // circuit4 Raise Stop
                            {
                                kitchenDimu4.DinLoads[args.Sig.Number - 10].LevelIn.StopRamp();
                                break;
                            }
                            case 15: // circuit1 Lower Stop
                            case 16: // circuit2 Lower Stop
                            case 17: // circuit3 Lower Stop
                            case 18: // circuit4 Lower Stop
                            {
                                kitchenDimu4.DinLoads[args.Sig.Number - 10].LevelIn.StopRamp();
                                break;
                            }  
                            default:
                                CrestronConsole.PrintLine("Button{0}'s Not Programmed!!\r\n", args.Sig.Number);   
                                break;
                        }
                    }

                    break;

                // a ushort (analog) has chagned
                case eSigType.UShort:
                    switch (args.Sig.Number)
                    {
                        case 1:
                            // send the slider value to the lamp dimmer

                            break;

                        default:
                            break;
                    }
                    break;


                case eSigType.String:
                case eSigType.NA:
                default:
                    break;
            }
        }

        // Sig callback for handling C2N-CBF-P button events.
        void myKp_ButtonChange(GenericBase currentDevice, ButtonEventArgs args)
        {
            CrestronConsole.PrintLine("Button{0}'s state is {1}\r\n", args.Button.Number, args.Button.State);

            var kp = (C2nCbfP)currentDevice;
            
            switch(args.Button.State)
            {
                case eButtonState.Pressed:
                {
                    CrestronConsole.PrintLine("Kp Btn{0} Pressed Switch-Case\r\n", args.Button.Number); 

                    switch (args.Button.Number)
                    {
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        {
                            CrestronConsole.PrintLine("Kp Btn{0} Pressed Case\r\n", args.Button.Number);
                            
                            for(ushort i=1; i<=4; i++)
                            {
                                CrestronConsole.PrintLine("Dimu4 Cir{0} Level = {1}\r\n", i, lightsDb.getCircuitValue(Convert.ToUInt16(0), Convert.ToUInt16(args.Button.Number - 1), Convert.ToUInt16(i-1)));
                                kitchenDimu4.DinLoads[i].LevelIn.CreateRamp(lightsDb.getCircuitValue(Convert.ToUInt16(0), Convert.ToUInt16(args.Button.Number - 1), Convert.ToUInt16(i - 1)), rampScene);                              
                            }

                            //fb (interlock)
                            kpBtnFb(kp, new uint[,] { { 1, 1 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } });                            

                            break; 
                        }
                        case 5:
                        {
                            for (ushort i = 1; i <= 4; i++)
                            {
                                CrestronConsole.PrintLine("Dimu4 Cir{0} Level = 0\r\n", i);
                                kitchenDimu4.DinLoads[i].LevelIn.CreateRamp(Convert.ToUInt16(0), rampScene);   
                            }

                            //fb (interlock)
                            kpBtnFb(kp, new uint[,] { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 1 } });  

                            break;
                        }
                        case 6: 
                        {
                            for (ushort i = 1; i <= 4; i++)
                            {
                                kitchenDimu4.DinLoads[i].LevelIn.CreateRamp(Convert.ToUInt16(0), rampRaiseLower);
                                CrestronConsole.PrintLine("Dimu4 Cir{0} Ramp Down Start\r\n", i);   
                            }

                            kpBtnFb(kp, new uint[,] { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } });  

                            break;
                        }
                        case 8:
                        {
                            kpBtnFb(kp, new uint[,] { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } });

                            for (ushort i = 1; i <= 4; i++)
                            {
                                kitchenDimu4.DinLoads[i].LevelIn.CreateRamp(Convert.ToUInt16(65535), rampRaiseLower);
                                CrestronConsole.PrintLine("Dimu4 Cir{0} Ramp Up Start\r\n", i);
                            }
                            break;
                        }

                        /*for (uint i = 1; i <= 8; i++)
                        {
                            if (args.Button.Number == currentDevice.[i].Number)
                            {
                                //This means Bool "sig" changed on the C2nCbdP
                                //Just print some information for now.
                                CrestronConsole.PrintLine("Button{0}'s state is {1}\r\n", i, args.Button.State);
                            }
                        }*/
                    }
                    break;
                }
            }
            switch (args.Button.State)
            {
                case eButtonState.Released:
                {
                    CrestronConsole.PrintLine("Kp Btn{0} Released Switch-Case\r\n", args.Button.Number); 

                    switch (args.Button.Number)
                    {
                        case 6:
                        {
                            for (ushort i = 1; i <= 4; i++)
                            {
                                kitchenDimu4.DinLoads[i].LevelIn.StopRamp();
                                CrestronConsole.PrintLine("Dimu4 Cir{0} Ramp Down Stop\r\n", i);
                            }
                            break;
                        }
                        case 8:
                        {
                            for (ushort i = 1; i <= 4; i++)
                            {
                                kitchenDimu4.DinLoads[i].LevelIn.StopRamp();
                                CrestronConsole.PrintLine("Dimu4 Cir{0} Ramp Up Stop\r\n", i);
                            }
                            break;
                        }                        
                    }
                    break;
                }
            }
        }
    }
}
