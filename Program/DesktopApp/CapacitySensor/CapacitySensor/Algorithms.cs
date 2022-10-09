﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace CapacitySensor
{
    public static class Algorithms
    {
        public static List<int> ChargingProbes = new List<int>();
        public static List<int> DischargingProbes = new List<int>();

        public static double Tick = 62.5; // ns

        public static int TCNT_Min = 30;
        public static int TCNT_Max = (int)Math.Pow(2, 16 - 3);

        public static double DewPoint(double T, double RH)
        {
            return Math.Pow(RH / 100.0, 0.125) * (112 + 0.9 * T) + 0.1 * T - 112;
        }
        public static void ParseT(string Received)
        {
            double T = 0.0, RH = 0.0;
            var Parts = Received.Replace('.', ',').Split(' ');
            if (Received[0] == (char)Device.Commands.TEMP)
            {
                double.TryParse(Parts[1], out T);
                double.TryParse(Parts[3], out RH);
            }
            else if (Received[0] != (char)Device.Commands.SAMPLES)
            {
                double.TryParse(Parts[0], out T);
                double.TryParse(Parts[1], out RH);
            }

            MainForm.Instance.T = T;
            MainForm.Instance.RH = RH;
            MainForm.Instance.DP = DewPoint(T, RH);
            if (T > 0.0 && RH > 0.0)
            {
                MainForm.Instance.LBL_T.Text = string.Format("{0:0.0} °C", T);
                MainForm.Instance.LBL_RH.Text = string.Format("{0:0.0} % ", RH);
                MainForm.Instance.LBL_DP.Text = string.Format("{0:0.0} °C", MainForm.Instance.DP);
            }
            else
            {
                MainForm.Instance.LBL_T.Text = "-";
                MainForm.Instance.LBL_RH.Text = "-";
                MainForm.Instance.LBL_DP.Text = "-";
            }
            MainForm.Instance.LBL_T_Charts.Text = MainForm.Instance.LBL_T.Text;
            MainForm.Instance.LBL_RH_Charts.Text = MainForm.Instance.LBL_RH.Text;
            MainForm.Instance.LBL_DP_Charts.Text = MainForm.Instance.LBL_DP.Text;
        }
        public static void ParseC(string Received)
        {
            var Samples = Received.Split(' ');
            var SamplesIndex = (Received[0] == (char)Device.Commands.SAMPLES) ? 2 : 6;

            DischargingProbes.Clear();
            ChargingProbes.Clear();

            int Size = Samples.Length - SamplesIndex;
            int[] Probes = new int[Size];

            for (int i = SamplesIndex; i < Samples.Length; i++)
                Probes[i - SamplesIndex] = int.Parse(Samples[i]);

            for (int i = 0; i < Size - 1; i++)
            {
                if (i % 2 == 1)
                {
                    DischargingProbes.Add(Probes[i + 1]);
                }
                else
                {
                    ChargingProbes.Add(Probes[i + 1]);
                }
            }
            //var DP = Tick * Oversampling(DischargingProbes);
            //var CP = Tick * Oversampling(ChargingProbes);
            List<int> ToDelete = new List<int>();
            var max = DischargingProbes.Max();
            double correction = 0.95;
            foreach (var value in DischargingProbes)
                if (value < correction * max)
                    ToDelete.Add(value);
            foreach (var delete in ToDelete)
                DischargingProbes.Remove(delete);
            ToDelete.Clear();
            max = ChargingProbes.Max();
            foreach (var value in ChargingProbes)
                if (value < correction * max)
                    ToDelete.Add(value);
            foreach (var delete in ToDelete)
                ChargingProbes.Remove(delete);

            var DP = Tick * DischargingProbes.Average();
            var CP = Tick * ChargingProbes.Average();

            var DP_Capacity = Capacity(DP, Calibration.R_MEAS, Calibration.J, Calibration.L_THR, Calibration.H_THR, Calibration.L_VOUT) * 1E3;
            var CP_Capacity = Capacity(CP, Calibration.R_MEAS, Calibration.J, Calibration.H_THR, Calibration.L_THR, Calibration.H_VOUT) * 1E3;

            DP_Capacity = double.Parse(string.Format("{0:0.0}", DP_Capacity));
            CP_Capacity = double.Parse(string.Format("{0:0.0}", CP_Capacity));

            //var CapacityAvg = Correction((DP_Capacity + CP_Capacity) / 2);
            var CapacityAvg = Correction(CP_Capacity);

            Console.WriteLine(string.Format("CP: {0:0.0}pF   {1}   DP: {2:0.0}pF  {3}      C: {4:0.0} pF\n", CP_Capacity, ChargingProbes.Count, DP_Capacity, DischargingProbes.Count, CapacityAvg));

            MainForm.Instance.C = CapacityAvg;
            MainForm.Instance.C2 = CapacityAvg;
            MainForm.Instance.LBL_C.Text = string.Format("{0:0.0} pF", CapacityAvg);
            MainForm.Instance.LBL_C_Charts.Text = string.Format("{0:0.0} pF", CapacityAvg);

            double RH = double.Parse(string.Format("{0:0.0}", CalcHumidity(CapacityAvg)));
            string strRH = string.Format("[{0:0.0} %]", RH);
            if (MainForm.Instance.LBL_RH.Text.Contains("-"))
            {
                MainForm.Instance.LBL_RH.Text = strRH;
                MainForm.Instance.LBL_RH_Charts.Text = strRH;
            }
            else
            {
                MainForm.Instance.LBL_RH.Text += $"\n{strRH}";
                MainForm.Instance.LBL_RH_Charts.Text += $"\n{strRH}";
            }
        }
        public static double Correction(double Capacity)
        {
            Capacity *= 1E-12;
            double CapacityCorr =
                //Calibration.A3 * 1E19 * Math.Pow(Capacity * 1E-12, 3) +
                //Calibration.A2 * 1E10 * Math.Pow(Capacity * 1E-12, 2) +
                //Calibration.A1 * Capacity * 1E-12 + 
                //Calibration.A0 * 1E-11;

                -4.644339E19 * Math.Pow(Capacity, 3) +
                2.793011E10 * Math.Pow(Capacity, 2) +
                -4.483682 * Capacity + 
                3.223107E-10;

            return CapacityCorr * 1E12;
        }
        public static double Oversampling(List<int> Probes)
        {
            int temp = 16 - (int)Math.Ceiling(Math.Log(Probes.Max(), 2));
            int bits = (temp < 4) ? temp : 3;
            int N = (int)Math.Pow(4, bits);
            long sum = 0;
            for (int i = Probes.Count - N; i < Probes.Count; i++)
                sum += Probes[i];
            double result = sum >> bits;
            return result * Math.Pow(2, -bits);
        }
        public static double ChargingTime(double C)
        {
            return Time(C, Calibration.R_MEAS, Calibration.J,
                Calibration.H_THR, Calibration.L_THR, Calibration.H_VOUT);
        }
        public static double DischargingTime(double C)
        {
            return Time(C, Calibration.R_MEAS, Calibration.J,
                Calibration.L_THR, Calibration.H_THR, Calibration.L_VOUT);
        }
        public static double Time(double CX, double RM, double JC, 
            double VCapStop, double VCapStart, double VOut)
        {
            return -CX * RM * Math.Log((VCapStop - VOut + JC * RM)/(VCapStart - VOut + JC * RM));
        }
        public static double Capacity(double T, double RM, double JC,
            double VCapStop, double VCapStart, double VOut)
        {
            return -T / RM / Math.Log((VCapStop - VOut + JC * RM) / (VCapStart - VOut + JC * RM));
        }

        public static double CalcHumidity(double HS1101_Capacity)
        {
            double HS1101_min = 164.0d;
            double HS1101_max = 201.0d;
            double RH = (HS1101_Capacity - HS1101_min) / (HS1101_max - HS1101_min) * 100.0d;
            if (RH < 0.0) RH = 0.0;
            if (RH > 100.0) RH = 100.0;
            return RH;
        }
    }
}