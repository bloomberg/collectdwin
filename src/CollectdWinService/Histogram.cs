using System;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class Histogram
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly int[] _bins;
        private readonly int _numBins;
        private int _binSize;
        private double _max;
        private double _min;
        private int _num;
        private double _sum;

        public Histogram()
        {
            _min = _max = _sum = _num = 0;
            _numBins = 100;
            _bins = new int[100];
            _binSize = 8;
        }

        private int GetBin(double val)
        {
            var bin = (int) (val/_binSize);
            return (bin);
        }

        public void Resize(double val)
        {
            double requiredBinSize = val/_numBins;
            // to reduce frequent resizing, new bin size will be the the next nearest power of 2
            // eg: 16, 32, 64, 128, 256, 512, 1024, 2048, 5086
            var newBinSize = (int) Math.Pow(2, Math.Ceiling(Math.Log(requiredBinSize, 2)));
            int oldBinSize = _binSize;
            for (int i = 1; i < _numBins; i++)
            {
                val = i*oldBinSize;
                int newBin = (int) val/newBinSize;
                if (i == newBin)
                    continue;
                _bins[newBin] += _bins[i];
                _bins[i] = 0;
            }
            _binSize = newBinSize;
            Logger.Debug("Resize() - OldBinSize:{0} has been replaced with the NewBinSize:{1}", oldBinSize, newBinSize);
        }

        public void AddValue(double val)
        {
            int bin = GetBin(val);
            if (bin >= _numBins)
            {
                Resize(val);
                bin = GetBin(val);
                Logger.Debug("Got new bin:{0}", bin);
            }
            _bins[bin]++;

            _min = (_min > val) ? val : _min;
            _max = (_max < val) ? val : _max;
            _sum += val;
            _num++;
        }

        public double GetPercentile(float percent)
        {
            double percentUpper = 0;
            double percentLower = 0;
            double sum = 0;

            if (percent < 0 || percent > 100 || _num <= 0)
                return (0);

            int i;
            for (i = 0; i < _numBins; i++)
            {
                percentLower = percentUpper;
                sum += _bins[i];
                percentUpper = 100*(sum/_num);
                if (percentUpper >= percent)
                    break;
            }
            if (Math.Abs(percentUpper) < 0.01 || i >= _numBins)
                return (0);
            double valLower = i*_binSize;
            double valUpper = (i + 1)*_binSize;

            double val = (((percentUpper - percent)*valLower) + ((percent - percentLower)*valUpper))/
                         (percentUpper - percentLower);

            return (val);
        }

        public void Reset()
        {
            _min = _max = _sum = _num = 0;
            for (int i = 0; i < _numBins; i++)
                _bins[i] = 0;
        }

        public override string ToString()
        {
            String logstr = String.Format("Min:{0} Max:{1} Sum{2} Num{3}", _min, _max, _sum, _num);
            for (int i = 0; i < _numBins; i++)
                logstr += ", " + _bins[i];
            return (logstr);
        }
    }
}

// ----------------------------------------------------------------------------
// Copyright (C) 2015 Bloomberg Finance L.P.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// ----------------------------- END-OF-FILE ----------------------------------