namespace CSR.Core.Similar
{
    // Сравнение единиц одного вида: порог отбора кандидатов по отпечаткам (префиксный фильтр) и оценка пары
    public abstract class UnitComparer
    {
        public abstract double CandidateThreshold { get; }

        // Оценка пары или null - не пара
        public abstract double? Score(SimilarUnit left, SimilarUnit right);

        public static UnitComparer Create(UnitKind kind, SimilarOptions options)
        {
            return kind switch
            {
                UnitKind.ParameterSet or UnitKind.FieldSet => new SetComparer(options.SetThreshold, options.MinShared),
                UnitKind.Type => new TypeComparer(options.TypeThreshold, options.Weights),
                UnitKind.System => new SystemComparer(options.SystemThreshold),
                _ => new ShapeComparer(options.Threshold),
            };
        }

        public static double GetJaccard(ulong[] left, ulong[] right)
        {
            int shared = CountShared(left, right);
            int union = left.Length + right.Length - shared;
            return union == 0 ? 0 : (double)shared / union;
        }

        // Общие элементы отсортированных массивов без повторов
        public static int CountShared(ulong[] left, ulong[] right)
        {
            int shared = 0;
            int i = 0;
            int j = 0;
            while (i < left.Length && j < right.Length)
            {
                if (left[i] == right[j])
                {
                    shared++;
                    i++;
                    j++;
                }
                else if (left[i] < right[j])
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }

            return shared;
        }
    }

    // L1: форма члена или фрагмента - Жаккар по отпечаткам не ниже порога
    public class ShapeComparer : UnitComparer
    {
        private readonly double m_Threshold;

        public ShapeComparer(double threshold)
        {
            m_Threshold = threshold;
        }

        public override double CandidateThreshold
            => m_Threshold;

        public override double? Score(SimilarUnit left, SimilarUnit right)
        {
            double score = GetJaccard(left.Fingerprints, right.Fingerprints);
            return score >= m_Threshold ? score : null;
        }
    }

    // L4: система - Жаккар меток окрестностей не ниже порога. Одна система, а не пара: корень одной внутри окрестности
    // другой или общие типы - больше половины меньшей окрестности (варианты одной точки расширения: у них общие база,
    // хранилище и потребители)
    public class SystemComparer : UnitComparer
    {
        // Доля общих типов меньшей окрестности, с которой две окрестности - одна система
        private const double SameSystemShare = 0.5;

        private readonly double m_Threshold;

        public SystemComparer(double threshold)
        {
            m_Threshold = threshold;
        }

        public override double CandidateThreshold
            => m_Threshold;

        public override double? Score(SimilarUnit left, SimilarUnit right)
        {
            if (left is not SystemUnit leftSystem || right is not SystemUnit rightSystem)
                return null;

            if (leftSystem.NodeKeys.Contains(rightSystem.RootKey) || rightSystem.NodeKeys.Contains(leftSystem.RootKey))
                return null;

            int shared = leftSystem.NodeKeys.Count(rightSystem.NodeKeys.Contains);
            if (shared > SameSystemShare * Math.Min(leftSystem.NodeKeys.Count, rightSystem.NodeKeys.Count))
                return null;

            double score = GetJaccard(left.Fingerprints, right.Fingerprints);
            return score >= m_Threshold ? score : null;
        }
    }

    // L2: набор параметров или полей - общих элементов не меньше minShared и Жаккар не ниже порога
    public class SetComparer : UnitComparer
    {
        private readonly double m_Threshold;
        private readonly int m_MinShared;

        public SetComparer(double threshold, int minShared)
        {
            m_Threshold = threshold;
            m_MinShared = minShared;
        }

        public override double CandidateThreshold
            => m_Threshold;

        public override double? Score(SimilarUnit left, SimilarUnit right)
        {
            int shared = CountShared(left.Fingerprints, right.Fingerprints);
            int union = left.Fingerprints.Length + right.Fingerprints.Length - shared;
            if (shared < m_MinShared || union == 0)
                return null;

            double score = (double)shared / union;
            return score >= m_Threshold ? score : null;
        }
    }
}
