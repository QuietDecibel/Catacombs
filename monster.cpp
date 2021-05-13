#include "monster.h"
#include "Dungeonspawner.h"
#include <QTimer>
#include <QGraphicsScene>
#include <QList>
#include <QRandomGenerator>
#include "stdlib.h"
#include "time.h"
#include <QDebug>
#include "PlaySquare.h"

using namespace std;

extern DungeonSpawner *DMSGame;
extern QGraphicsScene *theWorld;
extern PlaySquare *playR;
extern Monster * Enemy;

Monster::Monster(): QObject(), QGraphicsRectItem()
{
//Applies Color to and shading
    QPainter Filler;
    QPen Outter(Qt::black);
    QBrush Fill(Qt::red,Qt::SolidPattern);
    this->setBrush(Fill);
    this->setPen(Outter);

//Spawn Point Generator for Testing
    QRandomGenerator *randX = new QRandomGenerator;
    QRandomGenerator *randY = new QRandomGenerator;

    int ranX = randX->global()->bounded(0,780);
    int ranY = randY->global()->bounded(0,590);
    setRect(ranX,ranY,10,15);

//Creates a Monster using the constructor to be within the levels boundary.0
    QTimer * timer = new QTimer(this);
    connect(timer,SIGNAL(timeout()), this, SLOT(move()));
    timer->start(50);

//Changes the Monsters Direction at an interval.
    QTimer *Direction = new QTimer(this);
    connect(Direction,SIGNAL(timeout()), this, SLOT(Direction()));
    Direction->setInterval(2000);
    Direction->start();

//Collision for the Monster to the wall
    QTimer *HitWall = new QTimer(this);
    connect(Direction,SIGNAL(timeout()), this, SLOT(BumpIntoCollid()));
    HitWall->setInterval(50);
    HitWall->start(5);

//Create Stats for Monster
    int Specs = randX->global()->bounded(0,5);

    switch(Specs){
        case 0:
            this->setName("Goblin");
            this->setSpecies("Green Goblin");
            this->setHealth(randX->global()->bounded(50,100));
            this->setAttack(randX->global()->bounded(50,100));
            this->setDefense(randX->global()->bounded(50,100));
            this->setMagic(randX->global()->bounded(50,100));
            this->setCrit(randX->global()->bounded(50,100));
            break;
        case 1:
            this->setName("Wolf");
            this->setSpecies("Direwolf");
            this->setHealth(randX->global()->bounded(150,200));
            this->setAttack(randX->global()->bounded(150,200));
            this->setDefense(randX->global()->bounded(150,200));
            this->setMagic(randX->global()->bounded(150,200));
            this->setCrit(randX->global()->bounded(150,200));
            break;
        case 2:
            this->setName("Fire Spirit");
            this->setSpecies("Elemental");
            this->setHealth(randX->global()->bounded(250,300));
            this->setAttack(randX->global()->bounded(250,300));
            this->setDefense(randX->global()->bounded(250,300));
            this->setMagic(randX->global()->bounded(250,300));
            this->setCrit(randX->global()->bounded(250,300));
            break;
        case 3:
            this->setName("Banshee");
            this->setSpecies("Witch");
            this->setHealth(randX->global()->bounded(350,400));
            this->setAttack(randX->global()->bounded(350,400));
            this->setDefense(randX->global()->bounded(350,400));
            this->setMagic(randX->global()->bounded(350,400));
            this->setCrit(randX->global()->bounded(350,400));
            break;
        case 4:
            this->setName("Earth Golem");
            this->setSpecies("Elemental");
            this->setHealth(randX->global()->bounded(450,500));
            this->setAttack(randX->global()->bounded(450,500));
            this->setDefense(randX->global()->bounded(450,500));
            this->setMagic(randX->global()->bounded(450,500));
            this->setCrit(randX->global()->bounded(450,500));
            break;
        case 5:
            this->setName("Demon");
            this->setSpecies("Hell Spawn");
            this->setHealth(randX->global()->bounded(550,700));
            this->setAttack(randX->global()->bounded(550,600));
            this->setDefense(randX->global()->bounded(550,600));
            this->setMagic(randX->global()->bounded(650,700));
            this->setCrit(randX->global()->bounded(550,600));
            break;
    };

//Deletes Generators
    delete randX;
    delete randY;

}

void Monster::setName(string name)
{
    Name = name;
}

void Monster::setSpecies(string species)
{
    Species = species;
}

void Monster::setHealth(int health)
{
    Health = health;
}

void Monster::setMagic(int magic)
{
    Magic = magic;
}

void Monster::setAttack(int attack)
{
    Attack = attack;
}

void Monster::setDefense(int defense)
{
    Defense = defense;
}

void Monster::setCrit(double crit)
{
    Critical = crit;
}

string Monster::getName()
{
    return Name;
}

string Monster::getSpecies()
{
    return Species;
}

int Monster::getHealth()
{
    return Health;
}

int Monster::getMagic()
{
    return Magic;
}

int Monster::getAttack()
{
    return Attack;
}

int Monster::getDefense()
{
    return Defense;
}

double Monster::getCrit()
{
    return Critical;
}

int Monster::Direction()
{
    int choice = rand() % 5;
    int X = choice + 1;
    int Y = choice;
    if(choice == 0){
        return X;
    } else {
        return Y;
    }
}

void Monster::BumpIntoCollid()
{
    //Collision for Dungeon walls
    if(scene()->collidingItems(this).isEmpty()){
        //No Collision
    }else{
        //Collision
        setPos(x(), y());
    };
}

void Monster::move()
{
    //Moves the monster in a single direction.
    int rX = Direction();

    if(this->pos().x() > 0 || (this->pos().x() + 10) < 795){
        if(this->pos().y() > 0 || (this->pos().y() + 15) < 600){
            switch(rX){
                case 0:
                    setPos(x(),y()+5);
                    break;
                case 1:
                    setPos(x(),y()-5);
                    break;
                case 2:
                    setPos(x()+5,y());
                    break;
                case 3:
                    setPos(x()-5,y());
                    break;
                case 4:
                    setPos(x(),y()+5);
                    break;
                default:
                    setPos(x()+5,y());
                    break;

            };
        };
        if(this->pos().x() >= scene()->width() - 7 || this->pos().y() >= scene()->height() - 7){
            setX(scene()->width() - 7);
            setY(scene()->height() - 7);
        }
    };

}


