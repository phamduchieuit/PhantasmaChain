﻿using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class InteropContract : SmartContract
    {
        public override string Name => "interop";

        private StorageMap _hashes; 

        public InteropContract() : base()
        {
        }

        // receive from external chain
        public void DepositTokens(Hash hash, Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(destination != Address.Null, "invalid destination");
            Runtime.Expect(IsWitness(Runtime.Nexus.GenesisAddress), "invalid witness");

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

            var chainHashes = _hashes.Get<string, StorageSet>(symbol);
            Runtime.Expect(!chainHashes.Contains<Hash>(hash), "hash already seen");
            chainHashes.Add<Hash>(hash);

            var minimumAmount = UnitConversion.ToBigInteger(1, token.Decimals);
            // TODO this might have to smaller eg: for BTC
            Runtime.Expect(amount >= minimumAmount, "minimum amount not reached");

            var source = Address.Null;

            var balances = this.Runtime.Chain.GetTokenBalances(symbol);
            var supplies = token.IsCapped ? Runtime.Chain.GetTokenSupplies(symbol) : null;
            Runtime.Expect(Runtime.Nexus.MintTokens(symbol, this.Storage, balances, supplies, destination, amount), "mint failed");

            if (symbol == Nexus.StableTokenSymbol)
            {
                var feeAmount = minimumAmount / 10;
                Runtime.Expect(Runtime.Nexus.TransferTokens(symbol, this.Storage, balances, destination, Runtime.Chain.Address, feeAmount), "fee transfer failed");

                var fuelBalances = this.Runtime.Chain.GetTokenBalances(Nexus.FuelTokenSymbol);
                var fuelSupplies = token.IsCapped ? Runtime.Chain.GetTokenSupplies(Nexus.FuelTokenSymbol) : null;
                Runtime.Expect(Runtime.Nexus.MintTokens(Nexus.FuelTokenSymbol, this.Storage, fuelBalances, fuelSupplies, destination, feeAmount), "fee mint failed");
                // TODO this mint can only be done if the maximum supply was not reached yet
            }

            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        // send to external chain
        public void WithdrawTokens(Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(destination != Address.Null, "invalid destination");
            Runtime.Expect(IsWitness(Runtime.Nexus.GenesisAddress), "invalid witness");

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

            var source = Address.Null;

            var balances = this.Runtime.Chain.GetTokenBalances(symbol);
            var supplies = token.IsCapped ? Runtime.Chain.GetTokenSupplies(symbol) : null;
            Runtime.Expect(Runtime.Nexus.BurnTokens(symbol, this.Storage, balances, supplies, destination, amount), "burn failed");

            Runtime.Notify(EventKind.TokenSend, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }
    }
}
