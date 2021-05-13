#ifndef DUNGEONSPAWNER_H
#define DUNGEONSPAWNER_H

#include <QWidget>
#include <QGraphicsScene>
#include "PlaySquare.h"
#include "gamebar.h"
#include <QGraphicsView>
#include <QList>
#include "items.h"
#include "map.h"
#include "monster.h"

class DungeonSpawner: public QGraphicsView
{
public:
    DungeonSpawner(QWidget *parent=0);

    QGraphicsScene *theWorld;
    QGraphicsView *Level;
    PlaySquare *playR;
    GameBar *gameBar;
    Monster *Enemy;
    Map *Walls;
    QList<QObject *> LevelItems;
    QList<QObject *> LevelMonsters;

public slots:
    void Monster();
    void Item();

};

#endif // DUNGEONSPAWNER_H
