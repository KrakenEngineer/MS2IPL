Modular Spaceships 2 In-game Programming Language source code official leak

Technical documentation for modders (in Russian): https://docs.google.com/document/d/1YzODyDuB0A_xmTM85lbGAR4yKSFwlKD5vHhEDx7usHc/edit?tab=t.0#heading=h.c2pa4pezazhn

User doc (in Russian): https://docs.google.com/document/d/1mDAuBe3rjr-pt2npX4mr-FfcxjTqkAlyINwxJRKIKuc/edit?usp=sharing

Short description:
- MS2IPL is a programming language made to program spaceships
- The code will be written in a special part called "processor"
- Other parts will contain properties, that can be read and overwritten (like thrust of an engine) and methods (complex actions to execute)
- Processors will get data by reading properties and control ships by overwriting properties and calling methods

Implemented:
- Variables & expressions with strong typing
- 5 data types: int, float, bool, string, vector2
- Conditional statements: if-elif-else; switch
- Loops: always, while, for + break, continue
- Properties, methods, constructors
- Built-in library "std" for math, converting values to other types
- Some other utilities
- Bugs (some are known)

Safety features:
- Execution speed limit - will be implemented during integration into the game, yet supported
- Some lexycal, syntax and runtime errors are handled

Planned:
- Data structures (array, list, dictionary, stack, queue)
- User-defined functions, recursion, libraries
- Operators for functions (numerical derivatives, integrals, probably something else)
- Quality of life features
- Bugfixes
- Integration into the game

How to launch:
- Download the latest version from releases
- Put into separate folder
- Create code.ms2ipl
- Open code.ms2ipl, write your code
- Close, run the .exe
- See results in the console.txt
- The console also contains much debug information, errors. If there is a strange error or the error arises outside of console.txt, provide both console and code in a bug report
