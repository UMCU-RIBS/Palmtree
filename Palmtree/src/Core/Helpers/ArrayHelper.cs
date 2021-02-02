using System;

namespace Palmtree.Core.Helpers {

    public static class ArrayHelper {


        // secondDim = which position on the second dimension will be joined
        public static string jaggedArrayJoin(string seperator, string[][] array, int secondDimIndex) {
            string result = "";
            for (int i = 0; i < array.Length; i++) {
                if (i != 0) result += seperator;
                result += array[i][secondDimIndex];
            }
            return result;
        }

        // searchValue = string to search for
        // array = jaggged array of strings to search in
        // firstDimIndices  = if null, search all of the positions on the first dimension; if values > 0, then only those positions on the second dimension will be searched
        // secondDimIndices = if null, search all of the positions on the second dimension; if values > 0, then only those positions on the second dimension will be searched
        // returns    null if not found, 
        public static int[] jaggedArrayCompare(string searchValue, string[][] array, int[] firstDimIndices, int[] secondDimIndices, bool ignoreCase) {
            // TODO: rewrite recursive

            // create a return value
            int[] result = null;

            if (firstDimIndices == null) {
                for (int i = 0; i < array.Length; i++) {
                    if (secondDimIndices == null) {
                        for (int j = 0; j < array[i].Length; j++) {
                            if (string.Compare(searchValue, array[i][j], ignoreCase) == 0) {
                                result = new int[] { i, j };
                                break;
                            }
                        }
                    } else {
                        for (int iSD = 0; iSD < secondDimIndices.Length; iSD++) {
                            if (secondDimIndices[iSD] < array[i].Length && string.Compare(searchValue, array[i][secondDimIndices[iSD]], ignoreCase) == 0) {
                                result = new int[] { i, secondDimIndices[iSD] };
                                break;
                            }
                        }
                    }
                    if (result != null)     break;
                }
            } else {
                for (int iFD = 0; iFD < firstDimIndices.Length; iFD++) {
                    if (firstDimIndices[iFD] < array.Length) {
                        if (secondDimIndices == null) {
                            for (int j = 0; j < array[firstDimIndices[iFD]].Length; j++) {
                                if (string.Compare(searchValue, array[firstDimIndices[iFD]][j], ignoreCase) == 0) {
                                    result = new int[] { firstDimIndices[iFD], j };
                                    break;
                                }
                            }
                        } else {
                            for (int iSD = 0; iSD < secondDimIndices.Length; iSD++) {
                                if (secondDimIndices[iSD] < array[firstDimIndices[iFD]].Length && string.Compare(searchValue, array[firstDimIndices[iFD]][secondDimIndices[iSD]], ignoreCase) == 0) {
                                    result = new int[] { firstDimIndices[iFD], secondDimIndices[iSD] };
                                    break;
                                }
                            }
                        }
                        if (result != null)     break;
                    }
                }
                
            }
            return result;
        }
    }
            
}
