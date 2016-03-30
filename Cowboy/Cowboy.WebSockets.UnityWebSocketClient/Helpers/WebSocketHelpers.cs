namespace Cowboy.WebSockets
{
    internal sealed class WebSocketHelpers
    {
        internal static bool FindHttpMessageTerminator(byte[] buffer, int count, out int index)
        {
            index = -1;

            for (int i = 0; i < count; i++)
            {
                if (i + Consts.HttpMessageTerminator.Length <= count)
                {
                    bool matched = true;
                    for (int j = 0; j < Consts.HttpMessageTerminator.Length; j++)
                    {
                        if (buffer[i + j] != Consts.HttpMessageTerminator[j])
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
