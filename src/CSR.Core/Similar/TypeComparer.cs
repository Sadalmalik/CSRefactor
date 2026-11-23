namespace CSR.Core.Similar
{
    // Сопоставленные члены двух типов и оценка их формы L1
    public class MemberMatch
    {
        public required SimilarUnit Left { get; init; }

        public required SimilarUnit Right { get; init; }

        public required double Score { get; init; }
    }

    // L3: оценка пары типов - взвешенное среднее частей профиля (базы, переопределения, поля, сигнатуры - Жаккар)
    // и тел членов (жадное сопоставление по форме L1, вес пары - ее размер в узлах).
    // Часть, пустая у обоих типов, в среднее не входит. Кандидаты - по профилю целиком с порогом CandidateJaccard
    public class TypeComparer : UnitComparer
    {
        // Порог отбора кандидатов по профилю: типы одной схемы делят базы, переопределения и сигнатуры
        private const double CandidateJaccard = 0.3;

        private readonly double m_Threshold;
        private readonly TypeWeights m_Weights;

        public TypeComparer(double threshold, TypeWeights weights)
        {
            m_Threshold = threshold;
            m_Weights = weights;
        }

        public override double CandidateThreshold
            => CandidateJaccard;

        public override double? Score(SimilarUnit left, SimilarUnit right)
        {
            if (left is not TypeUnit leftType || right is not TypeUnit rightType)
                return null;

            return Compute(leftType, rightType, m_Threshold);
        }

        // Оценка пары без порога (пара по общему ядру)
        public double GetScore(TypeUnit left, TypeUnit right)
            => Compute(left, right, 0) ?? 0;

        // Оценка не ниже floor или null; тела не сравниваются, если даже полное совпадение тел не дотянет до floor
        private double? Compute(TypeUnit leftType, TypeUnit rightType, double floor)
        {
            double sum = 0;
            double weight = 0;
            Add(leftType.Profile.Bases, rightType.Profile.Bases, m_Weights.Bases, ref sum, ref weight);
            Add(leftType.Profile.Overrides, rightType.Profile.Overrides, m_Weights.Overrides, ref sum, ref weight);
            Add(leftType.Profile.Fields, rightType.Profile.Fields, m_Weights.Fields, ref sum, ref weight);
            Add(leftType.Profile.Signatures, rightType.Profile.Signatures, m_Weights.Signatures, ref sum, ref weight);

            bool hasBodies = leftType.Members.Count > 0 || rightType.Members.Count > 0;
            double bodiesWeight = hasBodies ? m_Weights.Bodies : 0;
            if (weight + bodiesWeight == 0 || (sum + bodiesWeight) / (weight + bodiesWeight) < floor)
                return null;

            if (hasBodies)
                sum += bodiesWeight * GetBodiesScore(Match(leftType, rightType), leftType, rightType);

            double score = sum / (weight + bodiesWeight);
            return score >= floor ? score : null;
        }

        // Жадное сопоставление членов: пары по убыванию оценки формы, каждый член - не больше одного раза
        public static List<MemberMatch> Match(TypeUnit left, TypeUnit right)
        {
            var candidates = new List<MemberMatch>();
            foreach (var leftMember in left.Members)
            {
                foreach (var rightMember in right.Members)
                {
                    double score = GetJaccard(leftMember.Fingerprints, rightMember.Fingerprints);
                    if (score > 0)
                        candidates.Add(new MemberMatch { Left = leftMember, Right = rightMember, Score = score });
                }
            }

            var usedLeft = new HashSet<SimilarUnit>();
            var usedRight = new HashSet<SimilarUnit>();
            var matches = new List<MemberMatch>();
            foreach (var candidate in candidates.OrderByDescending(candidate => candidate.Score))
            {
                if (usedLeft.Contains(candidate.Left) || usedRight.Contains(candidate.Right))
                    continue;

                usedLeft.Add(candidate.Left);
                usedRight.Add(candidate.Right);
                matches.Add(candidate);
            }

            return matches;
        }

        // Оценка тел с весом по размеру: пара членов дает оценку * (узлов слева + узлов справа), делится на узлы всех
        // членов обоих типов. Одинаковые короткие члены схемы (Name => "...") не перевешивают основную логику
        private static double GetBodiesScore(List<MemberMatch> matches, TypeUnit left, TypeUnit right)
        {
            double total = left.Members.Sum(member => member.NodeCount) + right.Members.Sum(member => member.NodeCount);
            double matched = matches.Sum(match => match.Score * (match.Left.NodeCount + match.Right.NodeCount));
            return total == 0 ? 0 : matched / total;
        }

        private static void Add(
            ulong[] left,
            ulong[] right,
            double partWeight,
            ref double sum,
            ref double weight)
        {
            if (left.Length == 0 && right.Length == 0)
                return;

            sum += partWeight * GetJaccard(left, right);
            weight += partWeight;
        }
    }
}
