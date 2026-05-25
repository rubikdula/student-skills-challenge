Act as an expert C# developer. I am working on a BTEC Unit 4 Programming assignment to build a Tournament Scoring System for a college "Student Skills Challenge". 

Please scaffold a clean, object-oriented C# Console Application based on these exact specifications:

### Data Architecture & Constraints:
- 4 Teams max, strictly 5 members per team (20 team competitors total).
- 20 Individual competitors max.
- 5 Events total (Mix of Sporting, Academic, Problem-Solving).
- Events must be explicitly typed as either a 'Team Event' or an 'Individual Event'.
- Support "Single Event Entry": A participant/team can have scores for only 1 event instead of all 5.

### Scoring Logic:
- Award points based on rank within an event (e.g., 1st = 10pts, 2nd = 8pts, 3rd = 6pts, 4th = 4th, etc.). 
- System must aggregate total scores and dynamically rank teams and individuals.

### Technical Requirements:
- Written in clean, modern C# as a Console Application.
- Use strongly-typed classes (e.g., Competitor, Team, Event, Score) with proper encapsulation.
- Include a simple text-based menu loop to:
  1. Add/View Competitors & Teams 
  2. Record Event Placements 
  3. Display Leaderboards (Separate Team and Individual standings) 
- Provide robust input validation to prevent crashes (e.g., entering text where numbers are expected).
- Use clear comments to explain the data structures for my assignment documentation.

Let's start by generating the core class structures and data models.