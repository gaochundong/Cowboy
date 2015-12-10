# Cowboy
Cowboy is a HTTP library for building HTTP based services.

Cowboy actually is a kind of Nancy-Lite which I shrink the code size of the [Nancy](https://github.com/NancyFx/Nancy) framework.
Nancy has elegant design and great extensibility, the design ideas such as "It just works", "Easily customizable", "Low ceremony" and "Low friction" are really cool and its really a pleasure to leverage the power of it into application design.

But what I need here is just an API service with super simple GET and POST, no extensibility needed, no customization needed, no view engine needed, no cookie and session needed, and then the IoC container also not needed. So just make it simple.
