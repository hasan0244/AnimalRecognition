using Microsoft.ML.Data;

namespace AnimalWinForms.Models
{
    public sealed class ModelInput
    {
        // Даваме абсолютен път към файла; пре-трансформът ще го прочете като bytes
        [LoadColumn(0)]
        public string ImagePath { get; set; } = "";
    }

    public sealed class ModelOutput
    {
        // Ако моделът е обучен с PredictedLabel – ще го получим тук
        public string PredictedLabel { get; set; } = string.Empty;

        // Вектор с вероятности за всеки клас (подредени по реда на Labels.txt)
        public float[] Score { get; set; } = System.Array.Empty<float>();
    }
}
