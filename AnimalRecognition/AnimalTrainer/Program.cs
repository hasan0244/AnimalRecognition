using System;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Transforms;
using Microsoft.ML.Vision;
using AnimalTrainer.Model;

namespace AnimalTrainer
{
    internal class Program
    {
        private static readonly string SolutionDir = GetSolutionDir();
        private static readonly string ProjectDir = Path.Combine(SolutionDir, "AnimalTrainer");
        private static readonly string DataDir = Path.Combine(ProjectDir, "Data");
        private static readonly string ModelsDir = Path.Combine(ProjectDir, "Models");
        private static readonly string Workspace = Path.Combine(ModelsDir, "tf_workspace");
        private static readonly string ModelPath = Path.Combine(ModelsDir, "animalModel.zip");
        private static readonly string LabelsPath = Path.Combine(ModelsDir, "Labels.txt");

        static void Main(string[] args)
        {
            // Скриваме INFO/WARN/ERROR от TensorFlow възможно най-рано
            Environment.SetEnvironmentVariable("TF_CPP_MIN_LOG_LEVEL", "3", EnvironmentVariableTarget.Process);

            Directory.CreateDirectory(ModelsDir);
            Directory.CreateDirectory(Workspace);

            Console.WriteLine($"Data dir: {DataDir}");

            var ml = new MLContext(seed: 42);

            // 1) Данни от подпапки (Label = име на папката)
            var rows = ImageRow.ReadFromFolders(DataDir).ToList();
            Console.WriteLine($"Samples found: {rows.Count}");
            if (rows.Count == 0)
            {
                Console.WriteLine("⚠ Няма изображения в Data/<КЛАС>/*.jpg|*.png|*.bmp.");
                return;
            }

            var data = ml.Data.LoadFromEnumerable(rows);
            var shuffled = ml.Data.ShuffleRows(data, seed: 42);

            // 2) Train / Validation / Test split
            var trainTest = ml.Data.TrainTestSplit(shuffled, testFraction: 0.1, seed: 42);
            var trainVal = ml.Data.TrainTestSplit(trainTest.TrainSet, testFraction: 0.1, seed: 42);
            var train = trainVal.TrainSet;
            var validation = trainVal.TestSet;
            var test = trainTest.TestSet;

            // 3) Подготовка извън pipeline, за да важи и за ValidationSet:
            // 3.1) Label -> LabelAsKey (фиксираме ключовете по train)
            var toKey = ml.Transforms.Conversion.MapValueToKey(
                            outputColumnName: "LabelAsKey",
                            inputColumnName: "Label",
                            keyOrdinality: ValueToKeyMappingEstimator.KeyOrdinality.ByValue)
                        .Fit(train);

            var trainKeyed = toKey.Transform(train);
            var valKeyed = toKey.Transform(validation);
            var testKeyed = toKey.Transform(test);

            // 3.2) ImagePath -> Image (raw bytes) – еднакво за всички дялове
            var toImage = ml.Transforms.LoadRawImageBytes(
                                outputColumnName: "Image",
                                imageFolder: DataDir,
                                inputColumnName: nameof(ImageRow.ImagePath))
                            .Fit(trainKeyed);

            var trainPrepared = toImage.Transform(trainKeyed);
            var valPrepared = toImage.Transform(valKeyed);
            var testPrepared = toImage.Transform(testKeyed);

            // 4) Pipeline – само тренерът и връщане от Key към стойност
            var pipeline =
                ml.MulticlassClassification.Trainers.ImageClassification(
                    new ImageClassificationTrainer.Options
                    {
                        FeatureColumnName = "Image",
                        LabelColumnName = "LabelAsKey",
                        ValidationSet = valPrepared,
                        Arch = ImageClassificationTrainer.Architecture.ResnetV250,
                        WorkspacePath = Workspace,
                        Epoch = 6,     // беше 3
                        BatchSize = 8,    // ако RAM позволява; при OOM върни на 8
                        LearningRate = 0.005f
                    })
                .Append(ml.Transforms.Conversion.MapKeyToValue(
                        outputColumnName: "PredictedLabel",
                        inputColumnName: "PredictedLabel"));

            // 5) Обучение – заглушаваме STDERR (TF пише там)
            Console.WriteLine("\nTraining...");
            var originalErr = Console.Error;
            ITransformer model;
            using (var nullWriter = new StreamWriter(Stream.Null) { AutoFlush = true })
            {
                try
                {
                    Console.SetError(nullWriter); // временно скриваме TF съобщенията
                    model = pipeline.Fit(trainPrepared);
                }
                finally
                {
                    Console.SetError(originalErr); // връщаме STDERR
                }
            }

            // 6) Оценка
            var preds = model.Transform(testPrepared);
            var metrics = ml.MulticlassClassification.Evaluate(
                              preds,
                              labelColumnName: "LabelAsKey",
                              scoreColumnName: "Score");

            Console.WriteLine($"\nTest metrics:");
            Console.WriteLine($"  AccMicro = {metrics.MicroAccuracy:P2}");
            Console.WriteLine($"  AccMacro = {metrics.MacroAccuracy:P2}");
            Console.WriteLine($"  LogLoss  = {metrics.LogLoss:F4}");

            // 7) Етикети (от имената на подпапките)
            File.WriteAllLines(LabelsPath, ImageRow.GetLabels(DataDir));

            // 8) Запис на модела
            ml.Model.Save(model, trainPrepared.Schema, ModelPath);
            Console.WriteLine($"\nSaved model: {ModelPath}");
            Console.WriteLine($"Labels: {LabelsPath}");
        }

        private static string GetSolutionDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AnimalRecognition.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
