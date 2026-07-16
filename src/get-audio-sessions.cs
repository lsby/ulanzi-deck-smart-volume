using System;
using System.Collections.Generic;
using VolumeOSD.Audio;

public class GetAudioSessions {
    public static void Main() {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var sessions = AudioManager.GetActiveSessions();
        foreach(var session in sessions) {
            Console.WriteLine(session.AppName);
        }
    }
}
