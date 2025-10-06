using PhantomGo.Core.Agents;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Services
{
    public class GameInfoService : INotifyPropertyChanged
    {
        private static readonly GameInfoService _instance = new GameInfoService();
        public static GameInfoService Instance => _instance;
        private GameInfoService() { }
        private string _blackTeamName = "黑方";
        public string BlackTeamName
        {
            get => _blackTeamName;
            set => SetProperty(ref _blackTeamName, value);
        }
        private IPlayerAgent _blackAgent;
        public IPlayerAgent BlackAgent
        {
            get => _blackAgent;
            set => SetProperty(ref _blackAgent, value);
        }
        private string _whiteTeamName = "白方";
        public string WhiteTeamName
        {
            get => _whiteTeamName;
            set => SetProperty(ref _whiteTeamName, value);
        }
        private IPlayerAgent _whiteAgent;
        public IPlayerAgent WhiteAgent
        {
            get => _whiteAgent;
            set => SetProperty(ref _whiteAgent, value);
        }
        private DateTime _eventDateTime;
        public DateTime EventDateTime
        {
            get => _eventDateTime;
            set => SetProperty(ref _eventDateTime, value);
        }
        private string _eventLocation;
        public string EventLocation
        {
            get => _eventLocation;
            set => SetProperty(ref _eventLocation, value);
        }
        private string _eventName;
        public string EventName
        {
            get => _eventName;
            set => SetProperty(ref _eventName, value);
        }
        private bool _isEventMode = false;
        public bool IsEventMode
        {
            get => _isEventMode;
            set => SetProperty(ref _isEventMode, value);
        }

        #region INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if(Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
