using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class CharacterStat : ISerializableType
{
    protected readonly CharacterStatId _id;

    protected object _value;

    public CharacterStatId Id => _id;

    public int Int
    {
        get { return (int)_value; }
        set {  _value = value; }
    }

    public float Float
    {
        get { return (float)_value; }
        set { _value = value; }
    }

    public CharacterStat(CharacterStatId id, int value)
    {
        _id = id;
        _value = value;
    }

    public CharacterStat(CharacterStatId id, float value)
    {
        _id = id;
        _value = value;
    }

    public CharacterStat Set(int value)
    {
        _value = value;

        return this;
    }

    public CharacterStat Set(float value)
    {
        _value = value;

        return this;
    }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(_id);

        if (_value is int)
        {
            writer.Write(0);
            writer.Write(Convert.ToInt32(_value));
        }
        else if (_value is float)
        {
            writer.Write(1);
            writer.Write(Convert.ToSingle(_value));
        }
    }

    public static implicit operator float(CharacterStat characterStat)
    {
        return (float)characterStat._value;
    }

    public static implicit operator int(CharacterStat characterStat)
    {
        return (int)characterStat._value;
    }

    public static implicit operator KeyValuePair<CharacterStatId, CharacterStat>(CharacterStat characterStat)
    {
        return new KeyValuePair<CharacterStatId, CharacterStat>(characterStat._id, characterStat);
    }
}