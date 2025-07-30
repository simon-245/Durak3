using UnityEngine;

public class Card
{
    public string Suit { get; set; }
    public string Rank { get; set; }
    public bool IsTrump { get; set; }
    public GameObject GameObject { get; set; }

    public Card(string suit, string rank)
    {
        Suit = suit;
        Rank = rank;
        IsTrump = false;
    }

    public int GetRankValue()
    {
        string[] ranks = { "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };
        return System.Array.IndexOf(ranks, Rank);
    }

    public override string ToString()
    {
        return $"{Rank} of {Suit}{(IsTrump ? " (Trump)" : "")}";
    }
}