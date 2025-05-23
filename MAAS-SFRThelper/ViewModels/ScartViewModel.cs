﻿using NLog.LayoutRenderers.Wrappers;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.ViewModels
{
    public class ScartViewModel : BindableBase
    {
		// GTV - structure list
		private string _gtvId;

		public string GtvId
		{
			get { return _gtvId; }
			set { SetProperty(ref _gtvId, value); }
		}

		public ObservableCollection<string> Structures { get; set; }

		// pick superior margin
		private int _supMargin;
		public int SupMargin
		{
			get { return _supMargin; }
            set { SetProperty(ref _supMargin, value); }
        }

		public List<int> SupMargins { get; set; }

		// Pick inferior margin
        private int _infMargin;
        public int InfMargin
        {
            get { return _infMargin; }
            set { SetProperty(ref _infMargin, value); }
        }

        public List<int> InfMargins { get; set; }

		// Pick dose per fraction
		private double _dosePerFraction;

		public double DosePerFraction
		{
			get { return _dosePerFraction; }
            set { SetProperty(ref _dosePerFraction, value); }
        }

        public List<int> Fractions { get; set; }

		private int _numberOfFractions;
        private StructureSet _structureSet;
        private PlanSetup _plan;
        private EsapiWorker _esapiWorker;

        public int NumberOfFractions
		{
			get { return _numberOfFractions; }
            set { SetProperty(ref _numberOfFractions, value); }
        }

		public DelegateCommand GenerateSTV {  get; set; }

        public ScartViewModel(EsapiWorker esapiWorker)
        {
            _esapiWorker = esapiWorker;
            _esapiWorker.RunWithWait(sc =>
            {
                _structureSet = sc.StructureSet;
                _plan = sc.PlanSetup;
            });

            Structures = new ObservableCollection<string>();
            BindingOperations.EnableCollectionSynchronization(Structures, this);
            Fractions = new List<int>() {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
            InfMargins = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20};
            SupMargins = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20};

            InfMargin = 5;
            SupMargin = 5;

            SetPlanProperties();
			SetStructures();
            GenerateSTV = new DelegateCommand(CreateSTV);
				
        }

        private void CreateSTV()
        {
            if (!string.IsNullOrEmpty(GtvId))
            {
                _esapiWorker.Run(sc =>
                {
                    sc.Patient.BeginModifications();
                    Structure gtv = GetGtvFromId();
                    double shrink = CalculateShrinkPercentage();
                    Structure stv = CreateSTVStructure(gtv, shrink);

                });
            }
        }

        private Structure GetGtvFromId()
        {
            return _structureSet.Structures.FirstOrDefault(s => s.Id == GtvId);
        }

        private void SetStructures()
        {
            _esapiWorker.Run(sc =>
            {
                if (_structureSet != null)
                {
                    foreach (var structure in _structureSet.Structures.OrderByDescending(s => s.DicomType == "GTV"))
                    {
                        Structures.Add(structure.Id);
                    }

                    if (_structureSet.Structures.Any(s => s.DicomType == "GTV" && !s.IsEmpty))
                    {
                        GtvId = _structureSet.Structures.First(s => s.DicomType == "GTV" && !s.IsEmpty).Id;
                    }
                }
            });
        }

        private void SetPlanProperties()
        {
            _esapiWorker.Run(sc =>
            {
                if (_plan != null)
                {
                    if (! Double.IsNaN (_plan.DosePerFraction.Dose) )
                    {
                        DosePerFraction = _plan.DosePerFraction.Dose;
                    }
                    else
                    {
                        DosePerFraction = _plan.TotalDose.Unit == DoseValue.DoseUnit.cGy ? 2400.0: 24.0;
                    }

                    if (_plan.NumberOfFractions != null)
                    {
                        NumberOfFractions = _plan.NumberOfFractions.Value;
                    }
                    else { NumberOfFractions = 1; }
                }
            });
        }

        // --------------------------------------------------------------------------------
        private double CalculateShrinkPercentage()
        {
            // Calculate shrink percentage based on tables from the presentation
            // Reference values from presentation:
            // 15Gy x3 -> 36% (P/A = 33%)
            // 18Gy x3 -> 27% (P/A = 28%)
            // 21Gy x3 -> 24% (P/A = 24%)
            // 24Gy x3 -> 21% (P/A = 21%)
            double dosepfx = _plan.TotalDose.Unit == DoseValue.DoseUnit.cGy ? DosePerFraction / 100.0 : DosePerFraction; 
            double totalDose = dosepfx * NumberOfFractions;
            double protectionDose = 5.0; // Standard protection dose per the presentation
            double protectionTotal = protectionDose * NumberOfFractions;

            // P/A ratio (Protection dose / Ablation dose)
            double ratio = protectionTotal / totalDose;

            // Linear approximation based on presentation data
            return ratio;
        }

        private Structure CreateSTVStructure(Structure gtvStructure, double shrinkPercentage)
        {
            // Create new structure for STV
            string stvId = GetUniqueStructureId(_structureSet, "STV");
            Structure stvStructure = _structureSet.AddStructure("CONTROL", stvId);

            // Set color to red
            stvStructure.Color = (Color)ColorConverter.ConvertFromString("Red");

            // Check slices containing the GTV structure
            var gtvContours = new List<VVector[]>();
            var gtvZValues = new List<int>();

            // Collect all GTV contours
           for (int index = 0; index < _structureSet.Image.ZSize; index++) 
            { 
                foreach (var slice in gtvStructure.GetContoursOnImagePlane(index))
                {
                    if (slice.Count() > 0)
                    {
                        gtvContours.Add(slice);
                        gtvZValues.Add(index);
                    }
                }
            }

            if (gtvContours.Count == 0)
            {
                MessageBox.Show("Selected GTV structure has no contours.");
                return null;
            }

            // Sort contours by Z-coordinate
            //var sortedZips = gtvZValues.Zip(gtvContours, (z, contour) => new { Z = z, Contour = contour })
            //                         .OrderBy(pair => pair.Z)
            //                         .ToList();

            //var sortedZValues = sortedZips.Select(pair => pair.Z).ToList();
            //var sortedContours = sortedZips.Select(pair => pair.Contour).ToList();

            // Calculate slice thickness based on consecutive Z values
            double sliceThickness = _structureSet.Image.ZRes;
            //if (sortedZValues.Count > 1)
            //{
            //    sliceThickness = Math.Abs(sortedZValues[1] - sortedZValues[0]);
            //}
            //else
            //{
            //    MessageBox.Show("Cannot determine slice thickness. Using default of 3mm.");
            //    sliceThickness = 3.0;
            //}

            // Apply superior and inferior margins
            int superiorSlicesToSkip = (int)Math.Ceiling(SupMargin / sliceThickness);
            int inferiorSlicesToSkip = (int)Math.Ceiling(InfMargin / sliceThickness);

            // Process each contour (excluding margins)
            for (int i = 0 + inferiorSlicesToSkip; i < gtvContours.Count - superiorSlicesToSkip; i++)
            {
                VVector[] gtvContour = gtvContours[i]; // sortedContours[i];
                int zValue = gtvZValues[i];

                // Create points list for this contour
                List<VVector> points = new List<VVector>();
                for (int j = 0; j < gtvContour.GetLength(0); j++)
                {
                    points.Add(new VVector(gtvContour[j].x, gtvContour[j].y, gtvContour[j].z));
                }

                // Calculate centroid
                double centroidX = 0, centroidY = 0;
                foreach (var point in points)
                {
                    centroidX += point.x;
                    centroidY += point.y;
                }
                centroidX /= points.Count;
                centroidY /= points.Count;

                // Convert points to polar coordinates
                List<Tuple<double, double, VVector>> polarPoints = new List<Tuple<double, double, VVector>>();
                foreach (var point in points)
                {
                    double dx = point.x - centroidX;
                    double dy = point.y - centroidY;
                    double theta = Math.Atan2(dy, dx);
                    double rho = Math.Sqrt(dx * dx + dy * dy);
                    polarPoints.Add(new Tuple<double, double, VVector>(theta, rho, point));
                }

                // Perform centroid optimization similar to MATLAB code
                for (int s = 0; s < 360; s++) // 360 samples as in original code
                {
                    // Find minimum distance point
                    var minRhoPair = polarPoints.OrderBy(p => p.Item2).First();
                    double minRho = minRhoPair.Item2;
                    double minTheta = minRhoPair.Item1;

                    // Find approximate opposite point
                    double maxTheta = minTheta + Math.PI; // 180 degrees in radians
                    if (maxTheta > Math.PI) maxTheta -= 2 * Math.PI;

                    // Find the closest point to the opposite angle
                    var maxIndex = 0;
                    var minDiff = double.MaxValue;
                    for (int j = 0; j < polarPoints.Count; j++)
                    {
                        var diff = Math.Abs(polarPoints[j].Item1 - maxTheta);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            maxIndex = j;
                        }
                    }

                    double maxRho = polarPoints[maxIndex].Item2;

                    // Adjust centroid slightly toward max point (0.01 factor like original code)
                    double avgRho = 0.01 * (maxRho - (maxRho + minRho) / 2);
                    double dX = avgRho * Math.Cos(maxTheta);
                    double dY = avgRho * Math.Sin(maxTheta);

                    centroidX += dX;
                    centroidY += dY;

                    // Recalculate polar coordinates
                    for (int j = 0; j < points.Count; j++)
                    {
                        double dx = points[j].x - centroidX;
                        double dy = points[j].y - centroidY;
                        double theta = Math.Atan2(dy, dx);
                        double rho = Math.Sqrt(dx * dx + dy * dy);
                        polarPoints[j] = new Tuple<double, double, VVector>(theta, rho, points[j]);
                    }
                }

                // Apply shrinkage to create STV contour
                List<VVector> stvPoints = new List<VVector>();
                foreach (var polarPoint in polarPoints)
                {
                    double theta = polarPoint.Item1;
                    double rho = polarPoint.Item2 * shrinkPercentage; // Apply shrinkage

                    // Convert back to Cartesian
                    double newX = centroidX + rho * Math.Cos(theta);
                    double newY = centroidY + rho * Math.Sin(theta);

                    stvPoints.Add(new VVector(newX, newY, zValue));
                }

                // Add contour to structure
                if (stvPoints.Count > 2) // Need at least 3 points for a valid contour
                {
                    stvStructure.AddContourOnImagePlane(stvPoints.ToArray(), zValue);
                }
            }

            return stvStructure;
        }

        private string GetUniqueStructureId(StructureSet structureSet, string baseId)
        {
            string id = baseId;
            int counter = 1;

            while (structureSet.Structures.Any(s => s.Id == id))
            {
                id = $"{baseId}_{counter}";
                counter++;
            }

            return id;
        }

    }
}
