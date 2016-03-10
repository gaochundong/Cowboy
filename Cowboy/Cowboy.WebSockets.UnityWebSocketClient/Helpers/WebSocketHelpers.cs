using System;
using System.Globalization;

namespace Cowboy.WebSockets
{
    internal sealed class WebSocketHelpers
    {
        internal static bool FindHeaderTerminator(byte[] buffer, int count, out int index)
        {
            index = -1;

            for (int i = 0; i < count; i++)
            {
                if (i + Consts.HeaderTerminator.Length <= count)
                {
                    bool matched = true;
                    for (int j = 0; j < Consts.HeaderTerminator.Length; j++)
                    {
                        if (buffer[i + j] != Consts.HeaderTerminator[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                    {
                        index = i;
                        return true;
                    }
                }
                else
                {
                    break;
                }
            }

            return false;
        }
    }
}
