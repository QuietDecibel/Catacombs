#ifndef MONSTER_H
#define MONSTER_H

#include <QGraphicsRectItem>
#include <QObject>
#include <string.h>

using namespace std;

class Monster:public QObject, public QGraphicsRectItem
{
    Q_OBJECT
private:
    string Name;
    string Species;
    int Health;
    int Magic;
    int Attack;
    int Defense;
    double Critical;

public:
    Monster();
    void setName(string name);
    void setSpecies(string species);
    void setHealth(int health);
    void setMagic(int magic);
    void setAttack(int attack);
    void setDefense(int Defense);
    void setCrit(double crit);
    string getName();
    string getSpecies();
    int getHealth();
    int getMagic();
    int getAttack();
    int getDefense();
    double getCrit();
public slots:
    void move();
    int Direction();
    void BumpIntoCollid();
};

#endif // MONSTER_H
