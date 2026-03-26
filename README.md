```md
# 🎰 ReelWords (AI-Generated Experiment)

This project is an experiment using **Claude Code Game Studios** to generate a small game from scratch.

👉 Source of the experiment:  
https://github.com/Donchitos/Claude-Code-Game-Studios

---

## 🧠 Idea

**ReelWords** is a word game inspired by slot machines.

- There are **6 reels**
- Each reel contains a **fixed sequence of characters** (not random)
- A reel only spins when the player uses its letter in a guess
- Otherwise, it remains unchanged

### Gameplay Features

- Turn-based gameplay  
- Optional **timer mode**  
- Word validation using a **trie data structure**  
- Each letter has a **specific score**  
- Final score is calculated based on the guessed word  

---

## 🤖 Why This Exists

This project was created to test how well AI (Claude) can:

- Generate a full game from a simple idea  
- Produce usable architecture and code  
- Handle iteration and debugging  

The initial idea is intentionally simple — something a mid-level developer could typically build in about an hour.

---

## 🛠 Tech Stack

- **Unity 6**  
- **C#**  
- **Claude API**  

---

## 📄 What AI Did Well

- Generated detailed **GDD & design documents**  
- Structured documentation cleanly  
- Broke down systems into modular components  

---

## ⚠️ What Went Wrong

This project highlights current limitations of AI-generated game development:

### Overengineering
- ~23 design files for a very small idea  
- Unnecessary abstractions  

### Code Quality Issues
- Large number of redundant `.cs` files (~50% unnecessary)  
- Poor cohesion between systems  

### Broken Iteration Loop
- Fixing one error often introduced many more  
- Input system repeatedly broke  

### Scene & UI Problems
- Scenes initially generated **empty**  
- UI is functional at best, but mostly broken/ugly  
- Menu buttons (Play/Quit) do not work  

### Gameplay Issues
- Input only partially works (Simulator mode only)  
- Game logic is incorrect (forces sequential letter selection)  
- Game Over state does not trigger properly  

---

## 🧪 Final State

- Project opens (after manual fixes)  
- Some input works in limited scenarios  
- Core gameplay loop is **not functional**  
- Requires significant manual intervention to become playable  

---

## 💭 Conclusion

> AI is not replacing game developers anytime soon 😄

While AI can:
- Help with structure  
- Generate ideas and boilerplate  

It currently struggles with:
- Maintaining consistency across systems  
- Debugging complex interactions  
- Delivering a working end-to-end product  

---

## 📦 Repository

The full project (including generated content and issues) is available here:

👉 *[Add your repo link here]*

---

## 🙌 Notes

This repo is intentionally left as-is to reflect the real output of the AI system — not a polished or production-ready project.
```
