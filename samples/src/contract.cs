using Neo.SmartContract.Framework;

public class TestContract : TokenContract
{
    public override byte Decimals() => 0;
    public override string Symbol() => "TEST";
}
