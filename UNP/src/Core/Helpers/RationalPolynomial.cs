using System;
using System.Numerics;

namespace UNP.Core.Helpers {
    // class to represent rational polynomials, ie a fraction of two polynomials
    class RationalPolynomial {

        // fields
        public Polynomial numerator;
        public Polynomial denominator;


        // constructor         
        public RationalPolynomial(Polynomial num, Polynomial denom) {

            // transfer variables
            numerator = num;
            denominator = denom;

            // simplify rational polynomial if possible
            simplify();
        }


        // simplify fraction if possible
        private void simplify() {
            if (numerator.rootsKnown && denominator.rootsKnown) Console.WriteLine("Warning: rational polynomial should be simplified before continuing. Check code.");          // TODO: implement C++ code Ratpoly<T>::Simplify() from Polynomials.h, only needed if model order is 1
        }


        // overload *= operator to define desired behavior
        public static RationalPolynomial operator *(RationalPolynomial rp, double f) {
            rp.numerator *= f;
            return rp;
        }


        public double evaluate(double f) {                  // TODO: see if faster with only real parts of complex
            return 0.0;
        }


        // check if complex value is close to zero, ie if both real and imaginary parts are closer to zero than epsilon
        public bool closeToZero(Complex c) {
            return closeToZero(c.Real) && closeToZero(c.Imaginary);
        }


        // check if double is close to zero, ie positive and smaller than epsilon
        public bool closeToZero(double d) {
            return Math.Abs(d) < Double.Epsilon;
        }


        public Complex evaluate(Complex z) {

            // determine num and denom
            Complex num = numerator.evaluate(z, 0);
            Complex denom = denominator.evaluate(z, 0);

            // if denom close to zero, adjust
            if (closeToZero(denom)) {

                int derivative = 0;

                while (closeToZero(denom) && derivative <= denominator.order() && closeToZero(num)) {
                    num = numerator.evaluate(z, derivative);
                    denom = denominator.evaluate(z, derivative);
                    ++derivative;
                }

                if (closeToZero(denom)) {
                    if (closeToZero(num)) {
                        num = 1;
                        denom = 1;
                    } else {
                        num = 1;
                        denom = Double.Epsilon;
                    }
                }
            }

            return num / denom;
        }
    }
}
