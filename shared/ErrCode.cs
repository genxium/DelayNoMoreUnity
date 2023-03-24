using System;
using System.Collections.Generic;
using System.Text;

namespace shared {
    public class ErrCode {
        // Using numeric constants instead of enums here to favor use of an ErrCode as a WebSocket custom close code; the value starts with 3000 to avoid clash with the "Defined Status Codes" in https://www.rfc-editor.org/rfc/rfc6455.html#section-7.4.1
		public const int Ok = 3000;                                               
		public const int UnknownError = 3001;                                     
		public const int IsTestAcc = 3002;                                        
		public const int IsBotAcc = 3003;                                         
		public const int DatabaseError = 3004;                                       
		public const int NonexistentAct = 3005;                                   
		public const int NonexistentActHandler = 3006;                           
		public const int LocallyNoAvailableRoom = 3007;                           
		public const int LocallyNoSpecifiedRoom = 3008;                           
		public const int PlayerNotAddableToRoom = 3009;                           
		public const int PlayerNotFound = 3010;                                   
		public const int PlayerNotReaddableToRoom = 3011;                         
		public const int SamePlayerAlreadyInSameRoom = 3012;                      
		public const int PlayerCheating = 3013;                                   
		public const int SmsCaptchaNotMatch = 3014;                               
		public const int SmsCaptchaRequestedTooFrequently = 3015;                 
        public const int InvalidAuthToken = 3016;                                     
		public const int ActiveWatchdog = 3018;                                   
		public const int BattleStopped = 3019;                                    
		public const int ClientMismatchedRenderFrame = 3020;                      
		public const int IncorrectCaptcha = 3021;                                 
		public const int IncorrectUname = 3022;                                 
		public const int IncorrectPassword = 3023;                                
		public const int IncorrectPhoneCountryCode = 3024;                        
		public const int IncorrectPhoneNumber = 3025;                             
		public const int InsufficientMemToAllocateConnection = 3026;              
		public const int InvalidRequestParam = 3027;                             
		public const int NewUnameConflict = 3028;

		public const int NotImplementedYet = 65535;
    }
}
