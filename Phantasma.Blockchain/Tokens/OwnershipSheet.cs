﻿using Phantasma.Blockchain.Storage;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Tokens
{
    public class OwnershipSheet
    {
        private byte[] _prefixItems;
        private byte[] _prefixOwner;

        public OwnershipSheet(string symbol)
        {
            this._prefixItems = Encoding.ASCII.GetBytes(symbol + ".ids.");
            this._prefixOwner = Encoding.ASCII.GetBytes(symbol + ".own.");
        }

        private byte[] GetKeyForList(Address address)
        {
            return ByteArrayUtils.ConcatBytes(_prefixItems, address.PublicKey);
        }

        private byte[] GetKeyForOwner(BigInteger tokenID)
        {
            return ByteArrayUtils.ConcatBytes(_prefixOwner, tokenID.ToByteArray());
        }

        public BigInteger[] Get(StorageContext storage, Address address)
        {
            lock (storage)
            {
                var listKey = GetKeyForList(address);
                var list = new StorageList(listKey, storage);
                return list.All<BigInteger>();
            }
        }

        public Address GetOwner(StorageContext storage, BigInteger tokenID)
        {
            lock (storage)
            {
                var ownerKey = GetKeyForOwner(tokenID);

                var temp = storage.Get(ownerKey);
                if (temp == null || temp.Length != Address.PublicKeyLength)
                {
                    return Address.Null;
                }

                return new Address(temp);
            }
        }

        public bool Give(StorageContext storage, Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (GetOwner(storage, tokenID) != Address.Null)
            {
                return false;
            }

            var listKey = GetKeyForList(address);
            var ownerKey = GetKeyForOwner(tokenID);

            lock (storage)
            {
                var list = new StorageList(listKey, storage);
                list.Add<BigInteger>(tokenID);

                storage.Put(ownerKey, address);
            }
            return true;
        }

        public bool Take(StorageContext storage, Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (GetOwner(storage, tokenID) != address)
            {
                return false;
            }

            var listKey = GetKeyForList(address);
            var ownerKey = GetKeyForOwner(tokenID);

            lock (storage)
            {
                var list = new StorageList(listKey, storage);
                list.Remove(tokenID);

                storage.Delete(ownerKey);
            }

            return true;
        }
    }
}
