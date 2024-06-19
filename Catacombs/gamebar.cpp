#include "gamebar.h"
#include "startwindow.h"
#include "PlaySquare.h"
#include "monster.h"
#include "Dungeonspawner.h"
#include <QFont>
#include <QDebug>

extern DungeonSpawner *DMSGame;
extern QGraphicsScene *theWorld;
extern PlaySquare *playR;
extern Monster *Enemy;

GameBar::GameBar(QGraphicsItem *parent): QGraphicsTextItem(parent)
{
    setPos(0,0);
    setHealth(200);
    setMagic(200);

    Heal = new QGraphicsTextItem;
    Magi = new QGraphicsTextItem;

    Heal->setPlainText("Health: " + QString::number(Health));
    Heal->setDefaultTextColor(Qt::red);
    Heal->setFont(QFont("Arial",12));
    Heal->setPos(5,0);
    qDebug() << Heal->pos();

    Magi->setPlainText("Magic: " + QString::number(Magic));
    Magi->setDefaultTextColor(Qt::blue);
    Magi->setFont(QFont("Arial",12));
    Magi->setPos((Heal->x()+100),(Heal->y()));
    qDebug() << Magi->pos();

    adjustSize();

};

void GameBar::decreaseHealth()
{
    //This May also be used as a basis to decrease health in the character class.
    //setHealth((DMSGame->Enemy->getAttack()/2) - playR->getHealth());
    //Heal->setPlainText(QString("Health") + QString::number(DMSGame->playR->getHealth()));
};

void GameBar::increaseHealth()
{
    Health = 25 + Health;
}

void GameBar::setHealth(int health)
{
    Health = health;
}

void GameBar::setMagic(int magic)
{
    Magic = magic;
};

int GameBar::getHealth()
{
    return Health;
};

int GameBar::getMagic()
{
    return Magic;
};
