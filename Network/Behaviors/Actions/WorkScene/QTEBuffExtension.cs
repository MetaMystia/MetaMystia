namespace MetaMystia.Network;

public static class QTEBuffExtension
{
    extension(QTEBuff buff)
    {
        public int ID => buff switch
        {
            QTEBuff.InstantEvaluation => 0,
            QTEBuff.PatientFreeze => 1,
            QTEBuff.ThrowDeliver => 2,

            QTEBuff.Fever => 3,
            QTEBuff.Fever_Infinite => -1,
            _ => 3,
        };
    }
}
