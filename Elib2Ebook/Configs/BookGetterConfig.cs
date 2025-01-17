﻿using System;
using System.Net;
using System.Net.Http;

namespace Elib2Ebook.Configs; 

public class BookGetterConfig : IDisposable {
    public HttpClient Client { get; }

    public Options Options { get; }
    
    public CookieContainer CookieContainer { get; }
    
    public bool HasCredentials => !string.IsNullOrWhiteSpace(Options.Login) && !string.IsNullOrWhiteSpace(Options.Password);

    public TempFolder.TempFolder TempFolder { get; }

    public BookGetterConfig(Options options, HttpClient client, CookieContainer cookieContainer, TempFolder.TempFolder tempFolder){
        Client = client;
        CookieContainer = cookieContainer;
        Options = options;
        TempFolder = tempFolder;
    }

    public void Dispose() {
        Client?.Dispose();
        TempFolder?.Dispose();
    }
}