using System;
using System.Numerics;
using NLog;

namespace UNP.Core.Helpers {

    // implements an ARFilter allowing power spectrum estimation using the Maximum Entropy Method, described in Numerical Recipes (Press et al.) and used in BCI2000
    public class ARFilter {

        // basic fields
        private const string CLASS_NAME = "ARFilter";
        private const int CLASS_VERSION = 1;
        private static Logger logger = LogManager.GetLogger(CLASS_NAME);

        // filter fields                                   
        private bool allowRun = false;               // flag to hold whether the settings in combination with the input data should allow the filter to run
        private double[] data;                       // incoming signal
        private double samplingFrequency;            // sampling frequency of incoming data
        private int modelOrder;                      // order of linear prediction model, ie number of coefficients in model
        private int numBins;                         // number of bins in power spectrum estimation
        private Complex[] lookupTable;               // lookupTable containg the normalized frequencies to asses power of
        private double[,] bins = null;               // array holding information of the bins for which power spectrum must be analysed 
        private RationalPolynomial linModel = null;                 //

        // filter porperties
        // read-only property for allowRun field, to see if filter can be used
        public bool AllowRun { get { return allowRun; } }

        // write-only property for data field, to allow data to be inserted into filter
        public double[] Data {
            set {
                if (value.Length < modelOrder) {
                    logger.Error("Length of input data (" + value.Length.ToString() + ") is too short for model order (" + modelOrder.ToString() + ").");
                    allowRun = false;
                } else {
                    data = value;
                    linModel = null;            // reset linear model, because this needs to be recalculated on new data
                    allowRun = true;
                }
            }
        }

   
        // create an ARFilter that processes an incoming signal, stored in data field, sampled at frequency sampleF, using a linear prediction model with numberCof coefficients; to do power spectrum estimation for bins defined by evalPerBin, lowerLimitBin, and upperLimitBin.
        public ARFilter(double sampleF, int numberCof, int[] evalPerBin, double[] lowerLimitBin, double[] upperLimitBin) {

            // transfer sampling frequency 
            if(sampleF > 0) samplingFrequency = sampleF;
            else {
                logger.Error("Can not construct ARFilter: given sampling frequency is not larger than 0.");
                allowRun = false;
            }

            // transfer model order
            if (numberCof >= 1) modelOrder = numberCof;
            else {
                logger.Error("Can not construct ARFilter: given model order is not at least 1.");
                allowRun = false;
            }
            
            // reset data field
            data = null;

            // if length of input arrays are equal and contain at least one element, aim to construct bin array
            if ((evalPerBin.Length == lowerLimitBin.Length) && (evalPerBin.Length == upperLimitBin.Length) && (evalPerBin.Length >= 1)) {

                // init vars to construct bin array
                numBins = evalPerBin.Length;
                bins = new double[numBins, 3];
                int amountLookups = 0;
               
                // fill bin array and determine total number of lookups if values pass sanity checks
                for (int i = 0; i < numBins; i++) {

                    // lower limit must be lower than  upper limit
                    if(lowerLimitBin[i] < upperLimitBin[i]) {
                        bins[i, 0] = lowerLimitBin[i];
                        bins[i, 1] = upperLimitBin[i];
                    } else {
                        logger.Error("Can not construct ARFilter: lower limit (" + lowerLimitBin[i] + ") for bin " + i + " is not smaller than the upper limit (" + upperLimitBin[i] + ").");
                        allowRun = false;
                    }

                    // evaluations per bin must be at least 1
                    if (evalPerBin[i] >= 1) {
                        bins[i, 2] = evalPerBin[i];
                        amountLookups += evalPerBin[i];
                    } else {
                        logger.Error("Can not construct ARFilter: evaluations per bin (" + evalPerBin[i] + ") for bin " + i + " is not at least 1.");
                        allowRun = false;
                    }  
                }

                // create lookupTable
                lookupTable = new Complex[amountLookups];

                // init vars to fill lookupTable
                int counter = 0;

                // fill lookupTable
                for (int bin = 0; bin < numBins; bin++) {

                    // determine distance between frequencies
                    double freqDist = ((bins[bin, 1] - bins[bin, 0]) / bins[bin, 2]);

                    // for each evaluation
                    for (int eval = 0; eval < (int)bins[bin, 2]; eval++) {

                        // TODO: currently, (as was done in BCI2000), the lower and uper limits are not the actual limits, these are 0.5*(binWidth/evalperBin) smaller on both sides
                        // frequencies to lookup 
                        double thetaDouble = (bins[bin, 0] + .5 * freqDist) + eval * freqDist;

                        // adjust frequencies for sampling frequency
                        thetaDouble = thetaDouble / samplingFrequency;

                        // convert with pi
                        thetaDouble = thetaDouble * 2.0 * Math.PI;

                        // make complex 
                        Complex thetaComplex = new Complex(1.0 * Math.Cos(thetaDouble), 1.0 * Math.Sin(thetaDouble));
                        lookupTable[counter] = thetaComplex;
                        counter++;
                    }
                }

                // ARFilter succesfully constructed
                allowRun = true;

            } else {
                logger.Error("Can not construct ARFilter: input arrays are not of equal length or contain less than 1 elements.");
                allowRun = false;
            }
        }


        // calculate the power spectrum of the incoming signal stored in data field by estimating linear model and applying the lookupTable on the model
        public double[] estimatePowerSpectrum() {

            // init power spectrum
            double[] powerSpectrum = new double[numBins];            

            // if filter is constructed properly and data filed is filled with suitable data
            if (allowRun && data != null && linModel != null) {

                // init variables
                int counter = 0;                                            // counter to fill powerspectrum array

                // multiply power by a factor of sqrt(2) to account for positive and negative frequencies.
                linModel = linModel * Math.Sqrt(2.0);

                // perform power spectrum estimation: cycle through bins, per bin calculate for each sample, ie frequencyresolution. (in BCI2000 this was transferspectrum.evaluate function, based loosely on function evlmem in Press et al.)
                for (int bin = 0; bin < numBins; ++bin) {

                    // init
                    powerSpectrum[bin] = 0.0;
                    int evalInBin = (int)bins[bin, 2];

                    // cycle through samples per bin
                    for (int eval = 0; eval < evalInBin; eval++) {

                        // get power estimate for given frequency in lookupTable and store in power spectrum output array
                        Complex valueComplex = linModel.evaluate(lookupTable[counter]);
                        powerSpectrum[bin] += ((valueComplex.Real * valueComplex.Real) + (valueComplex.Imaginary * valueComplex.Imaginary));         // it's actually the squared magnitude rather than a norm
                        counter++;
                    }

                    // normalize to the frequency resolution. NOTE: outputResult array holds spectral power. If interested in amplitude, take square root.
                    powerSpectrum[bin] /= evalInBin;
                }

                // output power spectrum estimation
                //logger.Debug("Power spectrum estimation of data:");
                //logger.Debug("Frequency bin \t Power");
                //for (int i = 0; i < numBins; i++) {
                //    double startBin = (firstBinCenter + (binWidth * i)) - (binWidth / 2);
                //    double endBin = (firstBinCenter + (binWidth * i)) + (binWidth / 2);
                //    logger.Debug("{0} - {1} Hz \t {2}", startBin, endBin, powerSpectrum[i]);
                //}

            } else { logger.Error("ARFilter is not constructed properly or input data is not suitable. Power spectrum can not be determined. See log files for more information."); }

            // return powerspectrum
            return powerSpectrum;
        }


        // create linear prediction model based on input signal (in 'Numerical Recipes'and in BCI2000 this function was called memCof)
        // NB. this alters the data contained in filter 
        public void createLinPredModel() {

            if (allowRun && data != null) {

                // init variables
                const double eps = Double.Epsilon;                          // minimal value that can be expressed in double type, used to check if value is close to zero
                double[] wkm = new double[modelOrder + 1];                  //
                double[] coeff = new double[modelOrder + 1];                // holds coefficients of to be created linear prediction model
                coeff[0] = 1.0;                                             // first coefficient of model is always 1

                int N = data.Length;                                        // length of input signal
                double[] mWk1 = new double[N];                              //
                double[] mWk2 = new double[N];                              //
                Array.Copy(data, mWk1, N);                                  // copy one array instead of assigning to prevent both variables pointing to same memory location (arrays are reference types)
                mWk2 = data;

                double meanPower = 0;                                       // mean power of signal
                double q = 1.0;                                             //


                // calculate mean power of input signal
                for (int t = 0; t < N; t++) meanPower += (mWk1[t] * mWk1[t]);

                // initialize numerator and denominator
                double num = 0.0;
                double den = meanPower * 2;

                // normalize mean power
                meanPower /= N;

                // calculate coefficients
                for (int k = 1; k <= modelOrder; ++k) {

                    // reset numerator
                    num = 0;

                    // calculate numerator and denominator 
                    for (int t = 0; t < N - k; t++) num += mWk1[t + 1] * mWk2[t];
                    den = den * q - mWk1[0] * mWk1[0] - mWk2[N - k] * mWk2[N - k];

                    // if denominator is close to zero (ie smaller than epsilon), set num and den to .5 and 1 respectively
                    if (den < eps) {
                        num = 0.5;
                        den = 1.0;
                    } else {
                        if (coeff[k] >= 1 || coeff[k] <= -1) {
                            den = 0;
                            for (int t = 0; t < N - k; t++) den += mWk1[t + 1] * mWk1[t + 1] + mWk2[t] * mWk2[t];
                        }
                    }

                    // set coefficient based on numerator and denominator
                    coeff[k] = 2 * num / den;

                    // update all coefficients
                    q = 1.0 - coeff[k] * coeff[k];
                    meanPower *= q;
                    for (int i = 1; i < k; ++i) coeff[i] = wkm[i] - coeff[k] * wkm[k - i];

                    // update wkm and mWk arrays
                    if (k < modelOrder) {
                        for (int i = 1; i <= k; ++i) wkm[i] = coeff[i];
                        for (int j = 0; j < N - k; ++j) {
                            mWk1[j] = mWk1[j + 1] - wkm[k] * mWk2[j];
                            mWk2[j] = mWk2[j] - wkm[k] * mWk1[j + 1];
                        }
                    }
                }

                // if mean power is below zero, set to zero (power can not be negative) 
                if (meanPower < 0.0) meanPower = 0.0;

                // update coefficients
                for (int k = 1; k <= modelOrder; k++) coeff[k] *= -1;

                // output linearmodel
                linModel = new RationalPolynomial(new Polynomial(Math.Sqrt(meanPower)), new Polynomial(coeff));

            } else { logger.Error("ARFilter is not constructed properly or input data is not suitable. Linear model can not be determined. See log files for more information."); }
        }
    }
}
