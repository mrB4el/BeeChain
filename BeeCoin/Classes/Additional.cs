using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeeCoin
{
    public class Additional
    {
        public MainWindow window;
        public bool debug = false;

        public void WindowInit(MainWindow main_window)
        {
            window = main_window;
        }

        public struct TwoBytesArrays
        {
            public byte[] part1;
            public byte[] part2;
        }
        public static byte[] ByteArrayJoin(TwoBytesArrays data)
        {
            byte[] resultArray = new byte[data.part1.Length + data.part2.Length];

            Array.Copy(data.part1, 0, resultArray, 0, data.part1.Length);
            Array.Copy(data.part2, 0, resultArray, data.part1.Length, data.part2.Length);

            return resultArray;
        }
        public static TwoBytesArrays ByteArrayCut(byte[] data, int part)
        {
            TwoBytesArrays result = new TwoBytesArrays();
            result.part1 = new byte[part];
            if (data.Length >= part) result.part2 = new byte[data.Length - part];

            if (data.Length >= part)
            {
                Array.Copy(data, result.part1, part);
                Array.Copy(data, part, result.part2, 0, result.part2.Length);
            }
            else result.part1 = data;

            return result;
        }
        public static byte[] IntToBytes(int intValue)
        {
            byte[] intBytes = BitConverter.GetBytes(intValue);
            byte[] result = intBytes;

            return result;
        }
        public static int BytesToInt(byte[] intBytes)
        {
            int result = 0;
            result = BitConverter.ToInt32(intBytes, 0);

            return result;
        }

        public static byte[] Uint64ToBytes(UInt64 argument)
        {
            byte[] uBytes = BitConverter.GetBytes(argument);
            return uBytes;
        }

        public static UInt64 BytesToUint64(byte[] uBytes)
        {
            UInt64 result = 0;
            result = BitConverter.ToUInt64(uBytes, 0);

            return result;
        }

        public byte[] OperationToBytes(string operation, int size)
        {
            byte[] data = new byte[0];

            while (operation.Length != size)
            {
                operation += "#";
            }

            data = Encoding.UTF8.GetBytes(operation);

            return data;
        }
        public string BytesToOperation(byte[] data)
        {
            string operation = "";

            operation = Encoding.UTF8.GetString(data);
            operation = operation.Trim('#');

            return operation;
        }

        public byte[] AddOperation(string operation, int size, byte[] data)
        {
            byte[] result = new byte[0];

            TwoBytesArrays array;
            array.part1 = OperationToBytes(operation, size);
            array.part2 = data;

            result = ByteArrayJoin(array);

            return result;
        }

        public byte[] ByteArrayFill(byte[] data, int size)
        {
            byte[] result = new byte[size];

            //Random r = new Random();

            //r.NextBytes(result);

            if (data.Length >= 0)
                Array.Copy(data, result, data.Length);

            return result;
        }

        public byte[] ByteArrayGet(byte[] data, int size)
        {
            byte[] result = new byte[size];

            if(data.Length >= size)
                Array.Copy(data, result, size);

            return result;
        }

        public struct Cortage
        {
            public string put;
            public int value;
        }

        public struct Transaction
        {
            public string name;
            public bool status;
            public int version;
            public Cortage input;
            public Cortage output;
            public string public_key;
            public int date;
            public byte[] signature;
            public byte[] information;
        }

        public struct Block
        {
            public string name;
            public int version;
            public int time;
            public string previous;
            public int transactions_count;
            public string root_hash;
            public int transactions_info_size;
            public string transactions_info;
            public UInt64 flowing;

            public byte[] transactions;
        }

        public int GetDate()
        {
            int unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            return unixTimestamp;
        }
    }
}
