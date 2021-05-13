#ifndef GAMEBAR_H
#define GAMEBAR_H
#include <QGraphicsTextItem>
#include <QObject>
#include <QFrame>
#include "PlaySquare.h"

class GameBar:public QGraphicsTextItem
{
private:
    int Health;
    int Magic;

public:
    GameBar(QGraphicsItem *parent=0);
    void decreaseHealth();
    void increaseHealth();
    void setHealth(int health);
    void setMagic(int magic);
    int getHealth();
    int getMagic();
    QGraphicsTextItem *Heal;
    QGraphicsTextItem *Magi;
};

#endif // GAMEBAR_H
