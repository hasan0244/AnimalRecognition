// AnimalWinForms/Services/AnimalClassifier.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.ML;

namespace AnimalWinForms.Services
{
    // Ако вече имаш тези класове в ModelTypes.cs – махни ги оттук
    public sealed class ModelInput
    {
        public string ImagePath { get; set; } = "";
    }

    public sealed class ModelOutput
    {
        public string PredictedLabel { get; set; } = "";
        public float[] Score { get; set; } = Array.Empty<float>();
    }

    public sealed class AnimalClassifier : IDisposable
    {
        private readonly MLContext _ml = new(seed: 42);
        private ITransformer? _model;
        private PredictionEngine<ModelInput, ModelOutput>? _engine;
        private string[] _labels = Array.Empty<string>();

        public AnimalClassifier()
        {
            // Пътища към модела и етикетите (в твоя WinForms проект: bin/*/Models)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var modelsDir = Path.Combine(baseDir, "Models");
            var modelPath = Path.Combine(modelsDir, "animalModel.zip");
            var labelsPath = Path.Combine(modelsDir, "Labels.txt");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Missing model file", modelPath);
            if (!File.Exists(labelsPath))
                throw new FileNotFoundException("Missing labels file", labelsPath);

            // Зареждаме .zip модела
            using (var fs = File.OpenRead(modelPath))
                _model = _ml.Model.Load(fs, out _);

            // ВАЖНО: вместо да четем схема, създаваме нужните входни колони
            // 1) ImagePath (string) -> Image (byte[])
            // 2) Дублираме като Feature и Features, за да покрием различни модели
            var pre = _ml.Transforms.LoadRawImageBytes(
                            outputColumnName: "Image",
                            inputColumnName: nameof(ModelInput.ImagePath),
                            imageFolder: "" // подаваме абсолютен път; root не е нужен
                        )
                        .Append(_ml.Transforms.CopyColumns("Feature", "Image"))
                        .Append(_ml.Transforms.CopyColumns("Features", "Image"))
                        .Fit(_ml.Data.LoadFromEnumerable(Array.Empty<ModelInput>()));

            var fullModel = pre.Append(_model);
            _engine = _ml.Model.CreatePredictionEngine<ModelInput, ModelOutput>(fullModel);

            _labels = File.ReadAllLines(labelsPath)
                          .Where(s => !string.IsNullOrWhiteSpace(s))
                          .Select(s => s.Trim())
                          .ToArray();
        }

        public IReadOnlyList<string> Labels => _labels;

        /// <summary>
        /// Връща Top-K (етикет, вероятност), сортирани по вероятност.
        /// </summary>
        public IEnumerable<(string label, float prob)> PredictTop(string imagePath, int k = 3)
        {
            if (_engine is null || _model is null)
                throw new InvalidOperationException("Model not loaded.");
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image not found", imagePath);

            var input = new ModelInput { ImagePath = imagePath };
            var output = _engine.Predict(input);

            if (output.Score is null || output.Score.Length == 0)
                return Enumerable.Empty<(string, float)>();

            // индекс -> (label, prob)
            return output.Score
                         .Select((p, idx) => (label: SafeLabel(idx), prob: p))
                         .OrderByDescending(x => x.prob)
                         .Take(Math.Max(1, k));
        }

        private string SafeLabel(int idx)
            => (idx >= 0 && idx < _labels.Length) ? _labels[idx] : $"class_{idx}";

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}
