/**
 * The Polynomial class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * Adapted from:        BCI2000 (Schalk Lab, www.schalklab.org) / Numerical Recipes in C (chapter 13, Press et al.)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System.Numerics;

namespace Palmtree.Core.Helpers {

    /// <summary>
    /// The <c>Polynomial</c> class.
    /// 
    /// Class to represent polynomials
    /// </summary>
    public class Polynomial {

        // fields: either represent the polynomial by defining its coefficients, or by defining its roots and constant factor
        public bool rootsKnown;                 // is the root of the polynomial known
        public double constantFactor;           // constant factor of polynomial
        public double[] roots;                  // roots of polynomial
        public double[] coefficients;           // coefficients of polynomial

        // constructor for polynomial consisting of only a constant factor
        public Polynomial(double d) {
            rootsKnown = true;                  // if there is only a constant factor, the root is known
            constantFactor = d;                 // set constant factor
        }


        // constructor for polynomial consisting of several coefficients // in BCI2000 this was method fromCoefficients() + constructor for use with vector) 
        public Polynomial(double[] coeffs) {

            // transfer coefficients
            coefficients = coeffs;

            // if there is only one coefficient, this polynomial has a constant factor equal to the first coefficent and the root is known, otherwise the roots are unknwon and the constant factor is 1 
            if (coefficients.Length == 1) {
                constantFactor = coefficients[0];
                rootsKnown = true;
            } else {
                rootsKnown = false;
                constantFactor = 1;
            }
        }


        // overload * operator to define desired behavior
        public static Polynomial operator *(Polynomial p, double f) {

            // if the roots are known, multiply the constant factor with the given factor f
            if (p.rootsKnown) p.constantFactor *= f;

            // multiply the coefficients with the given factor f
            if (p.coefficients != null && p.coefficients.Length != 0) for (int i = 0; i < p.coefficients.Length; ++i) p.coefficients[i] *= f;

            // return polynomial         
            return p;
        }


        // evaluate polynomial for given complex c and derivative d
        public Complex evaluate(Complex c, int d) {

            // init output
            Complex result = 0;

            // 
            if (rootsKnown && d == 0) {
                result = constantFactor;
                if (roots != null) for (int i = 0; i < roots.Length; ++i) result *= (c - roots[i]);
            } else {

                // compute coefficients and init vars
                computeCoefficients();
                Complex powerOfZ = 1;

                //
                for (int i = 0; i < coefficients.Length - d; ++i, powerOfZ *= c) result += coefficients[i + d] * powerOfZ;
            }

            // return
            return result;
        }


        // compute coefficients of polynomial
        public double[] computeCoefficients() {

            // if we know the roots and coefficients are not yet defined
            if (rootsKnown && (coefficients == null || coefficients.Length == 0)) {

                // init vars of coefficients
                coefficients = new double[roots.Length];
                coefficients[0] = 1;

                // one after one, multiply a factor of (z-mRoots[i]) into coeffs
                for (int i = 0; i < roots.Length; ++i) {
                    for (int j = coefficients.Length - 1; j >= 1; --j) {
                        coefficients[j] *= -(roots[i]);
                        coefficients[j] += coefficients[j - 1];
                    }
                    coefficients[0] *= -(roots[i]);
                }
            }

            return coefficients;
        }


        // return order of polynomial
        public int order() {

            // if the roots are known, return number of roots, otherwise number of coefficients minus 1
            if (rootsKnown) {
                if (roots != null) return roots.Length;
                else return 0;
            } else {
                if (coefficients != null) return coefficients.Length - 1;
                else return 0;
            }
        }

    }
}
