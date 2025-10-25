using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Services
{
    public class TimerService
    {
        private DispatcherTimer _moveTimer;
        private Stopwatch _stopwatch;
        public event Action<int> TimeUpdated;

        public TimerService()
        {
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _stopwatch = new Stopwatch();
            _moveTimer = new DispatcherTimer();
            _moveTimer.Interval = TimeSpan.FromSeconds(1);
            _moveTimer.Tick += MoveTimer_Tick;
        }
        private void MoveTimer_Tick(object sender, object e)
        {
            if (_stopwatch.IsRunning)
            {
                TimeUpdated?.Invoke((int)_stopwatch.Elapsed.TotalSeconds);
            }
        }
        public void StartTimer()
        {
            TimeUpdated?.Invoke(0);
            _stopwatch.Restart();
            _moveTimer.Start();
        }
        public void StopTimer()
        {
            _moveTimer.Stop();
            _stopwatch.Stop();
        }

        public void RestartTimer()
        {
            StopTimer();
            StartTimer();
        }

    }
}
